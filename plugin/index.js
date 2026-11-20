/**
 * 截图提问助手 · Host 半边
 *
 * 在 DSH 的本地 Web 服务上挂两个接口，供桌面浮窗调用：
 *   GET  /shot-ask/ping   健康检查，返回当前默认模型
 *   POST /shot-ask/ask    入参 { image, mediaType, question, history }，以 SSE 流式返回答案
 *
 * 模型、账号、额度全部复用 DSH 当前配置，无需额外 API Key。
 */

const PREFIX = '/shot-ask'
const MAX_BODY_BYTES = 16 * 1024 * 1024
const MAX_HISTORY_TURNS = 8

const SYSTEM_PROMPT = [
  '你是「截图提问助手」，一个陪用户看 PPT、看屏幕内容的助手。',
  '用户会发来一张截图（通常是 PPT 的一页、图表、公式、代码或文字），并可能附带问题。',
  '请用简体中文回答，先给结论或要点，再给必要的解释，语言简洁、条理清楚。',
  '如果图片内容看不清或信息不足，请直接说明，不要编造。',
].join('\n')

function sendJson(res, status, value) {
  const body = JSON.stringify(value)
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Cache-Control': 'no-store',
    'Content-Length': Buffer.byteLength(body),
  })
  res.end(body)
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = []
    let total = 0
    req.on('data', (chunk) => {
      total += chunk.length
      if (total > MAX_BODY_BYTES) {
        reject(new Error('请求内容过大（上限 16MB）'))
        req.destroy()
        return
      }
      chunks.push(chunk)
    })
    req.on('end', () => resolve(Buffer.concat(chunks)))
    req.on('error', reject)
  })
}

function decodeImage(image) {
  if (typeof image !== 'string' || image.length === 0) return undefined
  const comma = image.indexOf(',')
  const base64 = image.startsWith('data:') && comma >= 0 ? image.slice(comma + 1) : image
  const data = Buffer.from(base64, 'base64')
  return data.length > 0 ? data : undefined
}

function mediaTypeOf(input) {
  const allowed = new Set(['image/png', 'image/jpeg', 'image/webp', 'image/gif'])
  if (typeof input === 'string' && input.startsWith('image/')) {
    const normalized = input === 'image/jpg' ? 'image/jpeg' : input
    if (allowed.has(normalized)) return normalized
  }
  return 'image/png'
}

function historyText(history) {
  if (!Array.isArray(history) || history.length === 0) return ''
  const turns = history
    .filter((item) => item && typeof item.q === 'string' && item.q.trim().length > 0)
    .slice(-MAX_HISTORY_TURNS)
    .map((item, index) => {
      const answer = typeof item.a === 'string' ? item.a.trim() : ''
      return `第 ${index + 1} 轮\n问：${item.q.trim()}\n答：${answer || '（无）'}`
    })
  if (turns.length === 0) return ''
  return `以下是这次对话之前的内容，供你参考：\n\n${turns.join('\n\n')}\n\n—— 以上是历史，下面是用户的新问题。\n\n`
}

function chunkText(chunk) {
  if (!chunk || typeof chunk !== 'object') return undefined
  if (chunk.type === 'text-delta' && typeof chunk.text === 'string') return chunk.text
  return undefined
}

export const inject = ['webServer', 'llm', 'agentDefaultModel', 'attachments']

export function apply(ctx) {
  const logger = ctx.logger ? ctx.logger('shot-ask') : undefined

  ctx.effect(() =>
    ctx.webServer.register({
      kind: 'prefix',
      path: PREFIX,
      handler: (req, res) => handle(ctx, req, res, logger),
    }),
  )
}

async function handle(ctx, req, res, logger) {
  let url
  try {
    url = new URL(req.url ?? '/', 'http://localhost')
  } catch {
    sendJson(res, 400, { error: '非法请求地址' })
    return
  }

  if (url.pathname === `${PREFIX}/ping`) {
    const selection = ctx.agentDefaultModel.currentSelection()
    sendJson(res, 200, {
      ok: true,
      service: 'dsh-shot-ask',
      provider: selection?.provider ?? null,
      model: selection?.model ?? null,
    })
    return
  }

  if (url.pathname !== `${PREFIX}/ask`) {
    sendJson(res, 404, { error: '接口不存在' })
    return
  }
  if (req.method !== 'POST') {
    sendJson(res, 405, { error: '请使用 POST' })
    return
  }

  // ---- 解析请求 ----
  let payload
  try {
    const raw = await readBody(req)
    payload = JSON.parse(raw.toString('utf8'))
  } catch (error) {
    sendJson(res, 400, { error: `请求内容无法解析：${error?.message ?? error}` })
    return
  }

  const question = typeof payload?.question === 'string' ? payload.question.trim() : ''
  if (question.length === 0) {
    sendJson(res, 400, { error: '问题不能为空' })
    return
  }

  const imageBytes = decodeImage(payload?.image)
  if (!imageBytes) {
    sendJson(res, 400, { error: '缺少截图数据' })
    return
  }

  // ---- 保存图片（DSH 的附件服务会做格式校验与尺寸归一化）----
  let imageRef
  try {
    imageRef = await ctx.attachments.saveImage({
      data: imageBytes,
      mediaType: mediaTypeOf(payload?.mediaType),
      name: '截图.png',
    })
  } catch (error) {
    sendJson(res, 400, { error: `截图无法识别：${error?.message ?? error}` })
    return
  }

  const selection = ctx.agentDefaultModel.currentSelection()
  if (!selection?.provider || !selection?.model) {
    sendJson(res, 500, { error: '当前没有可用的默认模型，请先在 DSH 里选择模型' })
    return
  }

  const prefix = historyText(payload?.history)
  const messages = [
    {
      role: 'user',
      content: [
        { type: 'text', text: `${prefix}${question}` },
        { type: 'image', attachment: imageRef },
      ],
    },
  ]

  // ---- 流式回答 ----
  res.writeHead(200, {
    'Content-Type': 'text/event-stream; charset=utf-8',
    'Cache-Control': 'no-cache, no-transform',
    Connection: 'keep-alive',
    'X-Accel-Buffering': 'no',
  })
  res.write(': connected\n\n')
  res.write(`event: meta\ndata: ${JSON.stringify({ provider: selection.provider, model: selection.model })}\n\n`)

  const abort = new AbortController()
  const onClose = () => abort.abort()
  res.on('close', onClose)

  try {
    const stream = ctx.llm.stream({
      provider: selection.provider,
      model: selection.model,
      reasoningEffort: selection.reasoningEffort,
      system: SYSTEM_PROMPT,
      messages,
      signal: abort.signal,
    })

    for await (const chunk of stream) {
      if (abort.signal.aborted) break
      if (chunk?.type === 'reasoning-delta') continue
      const text = chunkText(chunk)
      if (text === undefined) continue
      res.write(`event: delta\ndata: ${JSON.stringify({ text })}\n\n`)
    }

    if (!abort.signal.aborted) {
      res.write('event: done\ndata: {}\n\n')
      res.end()
    }
  } catch (error) {
    logger?.warn?.(`回答失败: ${error?.message ?? error}`)
    if (!res.writableEnded) {
      res.write(`event: error\ndata: ${JSON.stringify({ message: String(error?.message ?? error) })}\n\n`)
      res.end()
    }
  } finally {
    res.off('close', onClose)
  }
}
