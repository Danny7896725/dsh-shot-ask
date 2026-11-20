using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ShotAsk
{
    internal sealed class MainForm : Form
    {
        private const int HotkeyId = 0x0B01;
        private const int WM_NOREPEAT = 0x4000;

        private readonly AppConfig _config;
        private readonly AskClient _client;

        private ShotPanel _preview;
        private Panel _inputHost;
        private Label _placeholder;
        private TextBox _input;
        private Panel _answerHost;
        private RichTextBox _answer;
        private Label _answerHint;
        private Label _status;
        private Panel _grip;
        private IconButton _btnCapture;
        private IconButton _btnMinimize;
        private IconButton _btnClose;
        private Button _btnSend;
        private Button _btnClear;
        private Button _btnCopyAnswer;
        private Button _btnRetake;
        private Button _btnCopyShot;
        private readonly List<ChipButton> _chips = new List<ChipButton>();
        private NotifyIcon _tray;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayTopmost;

        private readonly System.Windows.Forms.Timer _flushTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _captureTimer = new System.Windows.Forms.Timer();

        private Bitmap _shot;
        private string _shotBase64;
        private string _shotMediaType = "image/jpeg";
        private string _answerText = string.Empty;
        private string _lastQuestion = string.Empty;
        private bool _dirty;
        private bool _streaming;
        private bool _collapsed;
        private bool _quitting;
        private bool _hotkeyOk;
        private bool _capturing;
        private bool _resizing;
        private Point _resizeStart;
        private Rectangle _resizeBounds;
        private Rectangle _expandedBounds;
        private float _scale = 1f;
        private readonly List<KeyValuePair<string, string>> _history = new List<KeyValuePair<string, string>>();

        public MainForm()
        {
            _config = AppConfig.Load();
            _client = new AskClient(_config);

            using (var screen = Graphics.FromHwnd(IntPtr.Zero))
            {
                _scale = Math.Max(1f, screen.DpiX / 96f);
            }

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.FontUi;
            Text = "截图提问助手";
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            MinimumSize = new Size(S(360), S(460));
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _expandedBounds = _config.ResolveBounds(_scale);
            Bounds = _expandedBounds;

            BuildUi();
            BuildTray();

            _flushTimer.Interval = 70;
            _flushTimer.Tick += delegate { FlushAnswer(); };
            _captureTimer.Interval = 150;
            _captureTimer.Tick += delegate
            {
                _captureTimer.Stop();
                TakeShot();
            };

            _client.OnDelta = delegate(string text)
            {
                _answerText += text;
                _dirty = true;
            };
            _client.OnDone = delegate
            {
                SafeInvoke(delegate { FinishAnswer(false); });
            };
            _client.OnError = delegate(string message)
            {
                SafeInvoke(delegate { FinishAnswer(true, message); });
            };
        }

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            _preview = new ShotPanel();
            _preview.Click += delegate { BeginCapture(); };
            Controls.Add(_preview);

            _btnCapture = new IconButton(IconKind.Camera);
            _btnCapture.Click += delegate { BeginCapture(); };
            _btnCapture.GlyphColor = Theme.Accent;
            Controls.Add(_btnCapture);

            _btnMinimize = new IconButton(IconKind.Minus);
            _btnMinimize.Click += delegate { Collapse(); };
            Controls.Add(_btnMinimize);

            _btnClose = new IconButton(IconKind.Close);
            _btnClose.Click += delegate { HideToTray(); };
            Controls.Add(_btnClose);

            _inputHost = new Panel();
            _inputHost.BackColor = Theme.Panel;
            _inputHost.Padding = new Padding(S(12), S(9), S(12), S(9));
            _inputHost.Paint += PaintInputHost;
            Controls.Add(_inputHost);

            _input = new TextBox();
            _input.BorderStyle = BorderStyle.None;
            _input.BackColor = Theme.Panel;
            _input.ForeColor = Theme.Text;
            _input.Font = new Font("Microsoft YaHei UI", 9.5F);
            _input.Multiline = true;
            _input.ScrollBars = ScrollBars.None;
            _input.AcceptsReturn = true;
            _input.Dock = DockStyle.Fill;
            _input.KeyDown += InputKeyDown;
            _input.TextChanged += delegate
            {
                UpdatePlaceholder();
                _inputHost.Invalidate();
            };
            _input.GotFocus += delegate { UpdatePlaceholder(); _inputHost.Invalidate(); };
            _input.LostFocus += delegate { UpdatePlaceholder(); _inputHost.Invalidate(); };
            _inputHost.Controls.Add(_input);

            _placeholder = new Label();
            _placeholder.AutoSize = false;
            _placeholder.BackColor = Theme.Panel;
            _placeholder.ForeColor = Theme.Muted;
            _placeholder.Font = Theme.FontSmall;
            _placeholder.Text = "想问什么？例如：这页在讲什么 / 帮我总结一下要点";
            _placeholder.TextAlign = ContentAlignment.TopLeft;
            _placeholder.Cursor = Cursors.IBeam;
            _placeholder.Click += delegate { _input.Focus(); };
            _placeholder.Bounds = new Rectangle(S(12), S(12), 10, 24);
            _inputHost.Controls.Add(_placeholder);
            _placeholder.BringToFront();

            _btnSend = MakeFlatButton("发送", Theme.Accent, Color.White);
            _btnSend.Click += delegate { Send(); };
            Controls.Add(_btnSend);

            _btnClear = MakeFlatButton("清空", Theme.Background, Theme.Muted);
            _btnClear.Click += delegate { ClearConversation(); };
            Controls.Add(_btnClear);

            _answerHost = new Panel();
            _answerHost.BackColor = Theme.Panel;
            _answerHost.Padding = new Padding(S(10), S(8), S(10), S(8));
            _answerHost.Paint += PaintAnswerHost;
            Controls.Add(_answerHost);

            _answer = new RichTextBox();
            _answer.BorderStyle = BorderStyle.None;
            _answer.BackColor = Theme.Panel;
            _answer.ForeColor = Theme.Text;
            _answer.Font = Theme.FontAnswer;
            _answer.ReadOnly = true;
            _answer.DetectUrls = false;
            _answer.ScrollBars = RichTextBoxScrollBars.None;
            _answer.Dock = DockStyle.Fill;
            _answer.ShortcutsEnabled = true;
            _answer.MouseWheel += delegate(object sender, MouseEventArgs e)
            {
                Native.SendMessage(_answer.Handle, 0x0115, (IntPtr)(e.Delta > 0 ? 0 : 1), IntPtr.Zero);
            };
            _answerHost.Controls.Add(_answer);

            _answerHint = new Label();
            _answerHint.AutoSize = false;
            _answerHint.BackColor = Theme.Panel;
            _answerHint.ForeColor = Theme.Muted;
            _answerHint.Font = Theme.FontSmall;
            _answerHint.TextAlign = ContentAlignment.MiddleCenter;
            _answerHint.Text = "回答会显示在这里";
            _answerHint.Dock = DockStyle.Fill;
            _answerHost.Controls.Add(_answerHint);
            _answerHint.BringToFront();

            _status = new Label();
            _status.AutoSize = false;
            _status.BackColor = Theme.Background;
            _status.ForeColor = Theme.Muted;
            _status.Font = Theme.FontSmall;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Text = "正在连接 DSH…";
            Controls.Add(_status);

            _btnCopyAnswer = MakeFlatButton("复制回答", Theme.Background, Theme.Muted);
            _btnCopyAnswer.Click += delegate { CopyAnswer(); };
            _btnCopyAnswer.Font = Theme.FontSmall;
            Controls.Add(_btnCopyAnswer);

            _grip = new Panel();
            _grip.BackColor = Theme.Background;
            _grip.Cursor = Cursors.SizeNWSE;
            _grip.Size = new Size(16, 16);
            _grip.MouseDown += GripDown;
            _grip.MouseMove += GripMove;
            _grip.MouseUp += GripUp;
            _grip.Paint += PaintGrip;
            Controls.Add(_grip);
            _grip.BringToFront();

            _btnRetake = MakeFlatButton("重截", Theme.Background, Theme.Muted);
            _btnRetake.Click += delegate { BeginCapture(); };
            _btnRetake.Font = Theme.FontSmall;
            Controls.Add(_btnRetake);

            _btnCopyShot = MakeFlatButton("复制图", Theme.Background, Theme.Muted);
            _btnCopyShot.Click += delegate { CopyShot(); };
            _btnCopyShot.Font = Theme.FontSmall;
            Controls.Add(_btnCopyShot);

            string[] quick = _config.QuickList();
            for (int i = 0; i < quick.Length && i < 4; i++)
            {
                var chip = new ChipButton(quick[i]);
                string prompt = ChipPrompt(quick[i]);
                chip.Click += delegate { QuickAsk(prompt); };
                _chips.Add(chip);
                Controls.Add(chip);
            }

            Relayout();
        }

        private Button MakeFlatButton(string text, Color back, Color fore)
        {
            var button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = back;
            button.ForeColor = fore;
            button.Font = Theme.FontUi;
            button.Cursor = Cursors.Hand;
            button.TabStop = false;
            return button;
        }

        private static string ChipPrompt(string label)
        {
            switch (label)
            {
                case "解释": return "请解释截图里的内容，用通俗易懂的话说明它在讲什么。";
                case "总结": return "请总结截图里的要点，分条列出。";
                case "翻译": return "请把截图里的英文（或外文）内容翻译成中文，并保留原文关键术语。";
                case "出题": return "请根据截图里的内容出 2 道小测验题，并给出答案。";
                default: return "请针对截图内容回答：" + label;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_preview != null) Relayout();
        }

        private void Relayout()
        {
            if (_preview == null) return;
            if (_collapsed)
            {
                LayoutCollapsed();
                return;
            }
            int pad = 12;
            int width = (int)Math.Round(ClientSize.Width / _scale);
            int height = (int)Math.Round(ClientSize.Height / _scale);

            int headerTop = 6;
            _btnClose.Bounds = new Rectangle(width - pad - 30, headerTop + 2, 30, 30);
            _btnMinimize.Bounds = new Rectangle(_btnClose.Left - 32, headerTop + 2, 30, 30);
            _btnCapture.Bounds = new Rectangle(_btnMinimize.Left - 32, headerTop + 2, 30, 30);

            int previewTop = 44;
            int previewHeight = height >= 720 ? 176 : 150;
            _preview.Bounds = new Rectangle(pad, previewTop, width - pad * 2, previewHeight);

            int retakeY = _preview.Top + previewHeight - 30;
            _btnCopyShot.Bounds = new Rectangle(_preview.Right - pad - 52, retakeY, 52, 22);
            _btnRetake.Bounds = new Rectangle(_btnCopyShot.Left - 54, retakeY, 52, 22);

            int inputTop = _preview.Bottom + 10;
            int inputHeight = 86;
            _inputHost.Bounds = new Rectangle(pad, inputTop, width - pad * 2, inputHeight);
            _placeholder.Bounds = new Rectangle(S(12), S(10), _inputHost.ClientSize.Width - S(24), S(24));

            int rowTop = _inputHost.Bottom + 8;
            _btnSend.Bounds = new Rectangle(width - pad - 64, rowTop, 64, 30);
            _btnClear.Bounds = new Rectangle(_btnSend.Left - 52, rowTop, 46, 30);

            int chipLeft = pad;
            foreach (var chip in _chips)
            {
                chip.Bounds = new Rectangle(chipLeft, rowTop + 2, chip.LogicalWidth, 26);
                chipLeft += chip.LogicalWidth + 6;
            }

            int footerTop = height - pad - 22;
            int answerTop = rowTop + 38;
            _answerHost.Bounds = new Rectangle(pad, answerTop, width - pad * 2, Math.Max(80, footerTop - answerTop - 8));

            _status.Bounds = new Rectangle(pad + 2, footerTop, width - pad * 2 - 112, 22);
            _btnCopyAnswer.Bounds = new Rectangle(width - pad - 96, footerTop, 76, 22);
            _grip.Bounds = new Rectangle(width - 16, height - 16, 16, 16);
            _grip.Visible = true;
            ScaleAll();
            UpdatePlaceholder();
        }

        private int S(int value)
        {
            return (int)Math.Round(value * _scale);
        }

        private static Rectangle Scaled(Rectangle bounds, float scale)
        {
            return new Rectangle(
                (int)Math.Round(bounds.X * scale),
                (int)Math.Round(bounds.Y * scale),
                (int)Math.Round(bounds.Width * scale),
                (int)Math.Round(bounds.Height * scale));
        }

        private void ScaleAll()
        {
            if (Math.Abs(_scale - 1f) < 0.01f) return;
            Control[] controls =
            {
                _preview, _inputHost, _answerHost, _btnSend, _btnClear,
                _btnCapture, _btnMinimize, _btnClose, _btnCopyAnswer,
                _btnRetake, _btnCopyShot, _status, _grip,
            };
            foreach (var control in controls) control.Bounds = Scaled(control.Bounds, _scale);
            foreach (var chip in _chips) chip.Bounds = Scaled(chip.Bounds, _scale);
        }

        private void LayoutCollapsed()
        {
            int width = (int)Math.Round(ClientSize.Width / _scale);
            _btnClose.Bounds = new Rectangle(width - 38, 7, 30, 30);
            _btnCapture.Bounds = new Rectangle(width - 70, 7, 30, 30);
            _btnMinimize.Visible = false;
            ScaleAll();
        }

        private void UpdatePlaceholder()
        {
            if (_placeholder == null || _input == null) return;
            _placeholder.Visible = _input.TextLength == 0 && !_input.Focused;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (_collapsed || e.Y < 42)
            {
                Native.DragWindow(Handle);
            }
        }

        private void GripDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _resizing = true;
            _resizeStart = _grip.PointToScreen(e.Location);
            _resizeBounds = Bounds;
        }

        private void GripMove(object sender, MouseEventArgs e)
        {
            if (!_resizing) return;
            Point now = _grip.PointToScreen(e.Location);
            int width = Math.Max(MinimumSize.Width, _resizeBounds.Width + (now.X - _resizeStart.X));
            int height = Math.Max(MinimumSize.Height, _resizeBounds.Height + (now.Y - _resizeStart.Y));
            SetBounds(_resizeBounds.X, _resizeBounds.Y, width, height);
        }

        private void GripUp(object sender, MouseEventArgs e)
        {
            if (!_resizing) return;
            _resizing = false;
            SaveBounds();
        }

        private static void PaintGrip(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Theme.Muted, 1.1f))
            {
                for (int i = 0; i < 3; i++)
                {
                    int offset = 4 + i * 4;
                    g.DrawLine(pen, 14 - offset, 14, 14, 14 - offset);
                }
            }
        }

        private void PaintInputHost(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool focused = _input.Focused;
            using (var path = Theme.RoundRect(new Rectangle(0, 0, _inputHost.Width - 1, _inputHost.Height - 1), 10))
            using (var pen = new Pen(focused ? Color.FromArgb(140, Theme.Accent) : Theme.Border, 1.2f))
            {
                g.DrawPath(pen, path);
            }
        }

        private void PaintAnswerHost(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundRect(new Rectangle(0, 0, _answerHost.Width - 1, _answerHost.Height - 1), 10))
            using (var pen = new Pen(Theme.Border, 1f))
            {
                g.DrawPath(pen, path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Theme.Border, 1.2f))
            {
                g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }
            using (var brush = new SolidBrush(Theme.Text))
            {
                g.DrawString("截图提问助手", Theme.FontTitle, brush, S(16), S(_collapsed ? 13 : 12));
            }
            using (var dot = new SolidBrush(_hotkeyOk ? Color.FromArgb(0x4A, 0xD9, 0x8A) : Theme.Danger))
            {
                g.FillEllipse(dot, S(_collapsed ? 88 : 96), S(19), S(7), S(7));
            }
        }

        // -------------------------------------------------------------- 窗体行为

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.ApplyRoundedCorners(Handle);
            _hotkeyOk = RegisterHotkey();
            if (!_hotkeyOk)
            {
                SetStatus("热键 " + _config.Hotkey + " 被占用，可在 config.json 里改", Theme.Danger);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            CheckService();
            if (_config.Collapsed) Collapse();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                BeginCapture();
                return;
            }
            base.WndProc(ref m);
        }

        private bool RegisterHotkey()
        {
            int modifiers;
            Keys key;
            if (!HotkeyParser.TryParse(_config.Hotkey, out modifiers, out key)) return false;
            return Native.RegisterHotKey(Handle, HotkeyId, modifiers | WM_NOREPEAT, (int)key);
        }

        private void HideToTray()
        {
            SaveBounds();
            Hide();
            if (_tray != null)
            {
                _tray.BalloonTipTitle = "截图提问助手仍在后台";
                _tray.BalloonTipText = "按 " + _config.Hotkey + " 随时截图提问。";
                _tray.ShowBalloonTip(1500);
            }
        }

        private void ShowFromTray()
        {
            if (_collapsed) Expand();
            Show();
            Activate();
            BringToFront();
        }

        private void Collapse()
        {
            if (_collapsed) return;
            SaveBounds();
            _collapsed = true;
            _config.Collapsed = true;
            _config.Save();
            SuspendLayout();
            _preview.Visible = false;
            _inputHost.Visible = false;
            _answerHost.Visible = false;
            _btnSend.Visible = false;
            _btnClear.Visible = false;
            _btnRetake.Visible = false;
            _btnCopyShot.Visible = false;
            _btnCopyAnswer.Visible = false;
            _status.Visible = false;
            _grip.Visible = false;
            foreach (var chip in _chips) chip.Visible = false;
            Bounds = new Rectangle(Right - S(190), Top, S(190), S(44));
            _btnMinimize.Visible = false;
            ResumeLayout();
            Invalidate();
        }

        private void Expand()
        {
            if (!_collapsed) return;
            _collapsed = false;
            _config.Collapsed = false;
            _config.Save();
            SuspendLayout();
            _preview.Visible = true;
            _inputHost.Visible = true;
            _answerHost.Visible = true;
            _btnSend.Visible = true;
            _btnClear.Visible = true;
            _btnRetake.Visible = true;
            _btnCopyShot.Visible = true;
            _btnCopyAnswer.Visible = true;
            _status.Visible = true;
            _grip.Visible = true;
            _btnMinimize.Visible = true;
            foreach (var chip in _chips) chip.Visible = true;
            _btnMinimize.Kind = IconKind.Minus;
            Bounds = _expandedBounds;
            ResumeLayout();
            Relayout();
            Invalidate();
        }

        private void SaveBounds()
        {
            if (_collapsed) return;
            _expandedBounds = Bounds;
            _config.X = Bounds.X;
            _config.Y = Bounds.Y;
            _config.Width = Bounds.Width;
            _config.Height = Bounds.Height;
            _config.Save();
        }

        // -------------------------------------------------------------- 截屏

        private void BeginCapture()
        {
            if (_capturing) return;
            _capturing = true;
            if (_collapsed) Expand();
            Hide();
            _captureTimer.Start();
        }

        private void TakeShot()
        {
            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            Bitmap screen = null;
            try
            {
                screen = new Bitmap(virtualScreen.Width, virtualScreen.Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(screen))
                {
                    g.CopyFromScreen(virtualScreen.Location, Point.Empty, virtualScreen.Size, CopyPixelOperation.SourceCopy);
                }

                using (var overlay = new CaptureOverlay(screen, virtualScreen))
                {
                    DialogResult result = overlay.ShowDialog();
                    if (result == DialogResult.OK && overlay.Result != null)
                    {
                        SetShot(overlay.Result);
                    }
                }
            }
            catch (Exception error)
            {
                MessageBox.Show("截图失败：" + error.Message, "截图提问助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (screen != null) screen.Dispose();
                _capturing = false;
                Show();
                Activate();
                BringToFront();
                Native.SetForegroundWindow(Handle);
                BeginInvoke(new Action(delegate
                {
                    if (_collapsed) return;
                    _input.Focus();
                    _input.Select();
                }));
            }
        }

        private void SetShot(Bitmap shot)
        {
            int maxSide = _config.MaxImageSide;
            if (shot.Width > maxSide || shot.Height > maxSide)
            {
                float scale = Math.Min((float)maxSide / shot.Width, (float)maxSide / shot.Height);
                var resized = new Bitmap(Math.Max(1, (int)(shot.Width * scale)), Math.Max(1, (int)(shot.Height * scale)));
                using (var g = Graphics.FromImage(resized))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(shot, new Rectangle(0, 0, resized.Width, resized.Height));
                }
                shot.Dispose();
                shot = resized;
            }

            if (_shot != null) _shot.Dispose();
            _shot = shot;
            _preview.Shot = new Bitmap(shot);

            using (var buffer = new MemoryStream())
            {
                var codec = GetEncoder(ImageFormat.Jpeg);
                var parameters = new EncoderParameters(1);
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)_config.JpegQuality);
                shot.Save(buffer, codec, parameters);
                _shotBase64 = Convert.ToBase64String(buffer.ToArray());
                _shotMediaType = "image/jpeg";
            }

            SetStatus("已截取 " + shot.Width + " × " + shot.Height + "，输入问题后回车发送", Theme.Subtle);
            _history.Clear();
            _answerText = string.Empty;
            _answer.Clear();
            UpdateAnswerHint();
            _input.Focus();
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            foreach (var codec in ImageCodecInfo.GetImageEncoders())
            {
                if (codec.FormatID == format.Guid) return codec;
            }
            return null;
        }

        private void CopyShot()
        {
            if (_shot == null)
            {
                SetStatus("还没有截图", Theme.Muted);
                return;
            }
            try
            {
                Clipboard.SetImage(_shot);
                SetStatus("截图已复制到剪贴板", Theme.Subtle);
            }
            catch (Exception error)
            {
                SetStatus("复制失败：" + error.Message, Theme.Danger);
            }
        }

        private void CopyAnswer()
        {
            if (string.IsNullOrEmpty(_answerText))
            {
                SetStatus("还没有回答", Theme.Muted);
                return;
            }
            try
            {
                Clipboard.SetText(_answerText);
                SetStatus("回答已复制", Theme.Subtle);
            }
            catch (Exception error)
            {
                SetStatus("复制失败：" + error.Message, Theme.Danger);
            }
        }

        // -------------------------------------------------------------- 问答

        private void InputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                Send();
            }
        }

        private void QuickAsk(string prompt)
        {
            if (_shot == null)
            {
                SetStatus("先按 " + _config.Hotkey + " 截取一块区域", Theme.Accent);
                return;
            }
            _input.Text = prompt.Replace("请针对截图内容回答：", string.Empty);
            Send();
        }

        private void Send()
        {
            if (_streaming) return;
            if (_shotBase64 == null)
            {
                SetStatus("先按 " + _config.Hotkey + " 截取一块区域", Theme.Accent);
                return;
            }
            string question = _input.Text == null ? string.Empty : _input.Text.Trim();
            if (question.Length == 0)
            {
                SetStatus("先写下你的问题", Theme.Accent);
                _input.Focus();
                return;
            }

            _lastQuestion = question;
            _answerText = string.Empty;
            _answer.Clear();
            _answerHost.Visible = true;
            _streaming = true;
            _btnSend.Enabled = false;
            _btnSend.BackColor = Theme.PanelSoft;
            SetStatus("正在思考…", Theme.Subtle);
            UpdateAnswerHint();

            var history = new List<KeyValuePair<string, string>>();
            int depth = Math.Min(_config.HistoryDepth, _history.Count);
            for (int i = _history.Count - depth; i < _history.Count; i++) history.Add(_history[i]);

            _client.Ask(_shotBase64, _shotMediaType, question, history);
            _input.Clear();
        }

        private void FlushAnswer()
        {
            if (!_dirty) return;
            _dirty = false;
            _answer.SuspendLayout();
            string text = _answerText;
            _answer.Text = text;
            _answer.SelectionStart = _answer.TextLength;
            _answer.ScrollToCaret();
            _answer.ResumeLayout();
            UpdateAnswerHint();
        }

        private void UpdateAnswerHint()
        {
            if (_answerHint == null) return;
            bool empty = _answer == null || _answer.TextLength == 0;
            _answerHint.Visible = empty;
            if (empty)
            {
                _answerHint.Text = _streaming ? "正在思考…" : "回答会显示在这里";
            }
        }

        private void FinishAnswer(bool failed, string message = null)
        {
            _streaming = false;
            _btnSend.Enabled = true;
            _btnSend.BackColor = Theme.Accent;
            FlushAnswer();

            if (failed)
            {
                string error = string.IsNullOrEmpty(message) ? "请求失败" : message;
                _answerText = "⚠ " + error;
                _answer.ForeColor = Theme.Danger;
                _answer.Text = _answerText;
                _answer.ForeColor = Theme.Text;
                SetStatus("出错了", Theme.Danger);
                if (_lastQuestion.Length > 0 && _input.TextLength == 0)
                {
                    _input.Text = _lastQuestion;
                    _input.Select();
                }
                UpdateAnswerHint();
                return;
            }

            if (_lastQuestion.Length > 0)
            {
                _history.Add(new KeyValuePair<string, string>(_lastQuestion, _answerText));
                if (_history.Count > 20) _history.RemoveAt(0);
            }
            Markdown.Render(_answer, _answerText);
            UpdateAnswerHint();
            SetStatus("完成 · 可以继续追问", Theme.Muted);
            if (!_collapsed)
            {
                _input.Focus();
            }
        }

        private void ClearConversation()
        {
            if (_streaming) _client.Cancel();
            _streaming = false;
            _btnSend.Enabled = true;
            _btnSend.BackColor = Theme.Accent;
            _history.Clear();
            _answerText = string.Empty;
            _answer.Clear();
            UpdateAnswerHint();
            SetStatus("已清空，可以重新提问", Theme.Muted);
        }

        private void SetStatus(string text, Color color)
        {
            if (_status == null) return;
            _status.Text = text;
            _status.ForeColor = color;
        }

        private void SafeInvoke(Action action)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch (Exception) { }
        }

        private void CheckService()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                string model;
                string error = AskClient.Ping(_config.PingEndpoint, out model);
                SafeInvoke(delegate
                {
                    if (error == null)
                    {
                        SetStatus("已连接 DSH · " + (model ?? "模型未知"), Theme.Muted);
                        _config.Save();
                    }
                    else
                    {
                        SetStatus("未连接 DSH，请先启动 dsh web", Theme.Danger);
                    }
                });
            });
        }

        // -------------------------------------------------------------- 托盘

        private void BuildTray()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("显示浮窗", null, delegate { ShowFromTray(); });
            _trayMenu.Items.Add("截图提问", null, delegate { BeginCapture(); });
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayTopmost = new ToolStripMenuItem("始终置顶", null, delegate
            {
                TopMost = !TopMost;
                _trayTopmost.Checked = TopMost;
            });
            _trayTopmost.Checked = true;
            _trayMenu.Items.Add(_trayTopmost);

            var autoStart = new ToolStripMenuItem("开机自启动", null, delegate { ToggleAutoStart(); });
            autoStart.Checked = IsAutoStartEnabled();
            _trayAutoStart = autoStart;
            _trayMenu.Items.Add(autoStart);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("打开配置目录", null, delegate
            {
                try { System.Diagnostics.Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory); }
                catch (Exception) { }
            });
            _trayMenu.Items.Add("退出", null, delegate { Quit(); });

            _tray = new NotifyIcon();
            _tray.Text = "截图提问助手 · " + _config.Hotkey + " 截图提问";
            _tray.Icon = LoadAppIcon();
            _tray.ContextMenuStrip = _trayMenu;
            _tray.Visible = true;
            _tray.DoubleClick += delegate { ShowFromTray(); };
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null) return icon;
            }
            catch (Exception) { }
            var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(Theme.Accent))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(brush, 1, 1, 30, 30);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private const string AutoStartKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AutoStartValueName = "DSH-ShotAsk";
        private ToolStripMenuItem _trayAutoStart;

        private static bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(AutoStartKeyPath))
                {
                    return key != null && key.GetValue(AutoStartValueName) != null;
                }
            }
            catch (Exception) { return false; }
        }

        private void ToggleAutoStart()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(AutoStartKeyPath))
                {
                    if (key == null) return;
                    if (IsAutoStartEnabled())
                    {
                        key.DeleteValue(AutoStartValueName, false);
                        SetStatus("已关闭开机自启动", Theme.Muted);
                    }
                    else
                    {
                        key.SetValue(AutoStartValueName, "\"" + Application.ExecutablePath + "\"");
                        SetStatus("已设置开机自启动", Theme.Muted);
                    }
                }
                if (_trayAutoStart != null) _trayAutoStart.Checked = IsAutoStartEnabled();
            }
            catch (Exception error)
            {
                SetStatus("设置失败：" + error.Message, Theme.Danger);
            }
        }

        private void Quit()
        {
            _quitting = true;
            _client.Cancel();
            if (_tray != null) _tray.Visible = false;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_quitting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            SaveBounds();
            Native.UnregisterHotKey(Handle, HotkeyId);
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            base.OnFormClosing(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && !_collapsed) HideToTray();
            base.OnKeyDown(e);
        }
    }

    internal static class HotkeyParser
    {
        public static bool TryParse(string text, out int modifiers, out Keys key)
        {
            modifiers = 0;
            key = Keys.None;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (i == parts.Length - 1)
                {
                    Keys parsed;
                    if (!Enum.TryParse(part, true, out parsed)) return false;
                    key = parsed;
                }
                else
                {
                    switch (part.ToLowerInvariant())
                    {
                        case "ctrl": case "control": modifiers |= Native.MOD_CONTROL; break;
                        case "alt": modifiers |= Native.MOD_ALT; break;
                        case "shift": modifiers |= Native.MOD_SHIFT; break;
                        case "win": modifiers |= Native.MOD_WIN; break;
                        default: return false;
                    }
                }
            }
            return key != Keys.None;
        }
    }
}
