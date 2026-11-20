// 测试脚本：向截图提问助手接口发送一张图片 + 问题，打印流式返回。
// 用法：node scripts/test-ask.mjs <图片路径> "<问题>"
import { readFileSync } from 'node:fs'

const [, , file = 'docs/demo-slide.png', question = '这页 PPT 在讲什么？'] = process.argv
const bytes = readFileSync(file)
const image = `data:image/png;base64,${bytes.toString('base64')}`

const started = Date.now()
const res = await fetch('http://127.0.0.1:3080/shot-ask/ask', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ image, mediaType: 'image/png', question }),
})

console.log('HTTP', res.status, res.headers.get('content-type'))

const decoder = new TextDecoder()
let buffer = ''
let text = ''
for await (const chunk of res.body) {
  buffer += decoder.decode(chunk, { stream: true })
  let index
  while ((index = buffer.indexOf('\n\n')) >= 0) {
    const block = buffer.slice(0, index)
    buffer = buffer.slice(index + 2)
    let event = 'message'
    let data = ''
    for (const line of block.split('\n')) {
      if (line.startsWith('event: ')) event = line.slice(7)
      else if (line.startsWith('data: ')) data += line.slice(6)
    }
    if (event === 'meta') console.log('META', data)
    else if (event === 'delta') {
      const { text: piece } = JSON.parse(data)
      text += piece
      process.stdout.write(piece)
    } else if (event === 'error') console.log('\nERROR', data)
    else if (event === 'done') console.log('\n\nDONE in', Date.now() - started, 'ms, chars =', text.length)
  }
}
