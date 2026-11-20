using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShotAsk
{
    internal sealed class CaptureOverlay : Form
    {
        private readonly Bitmap _screen;
        private readonly Rectangle _origin;
        private Point _start;
        private Point _current;
        private bool _dragging;
        private bool _hasSelection;

        public Bitmap Result { get; private set; }

        public CaptureOverlay(Bitmap screen, Rectangle origin)
        {
            _screen = screen;
            _origin = origin;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(origin.Left, origin.Top, screen.Width, screen.Height);
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Black;
            Opacity = 1.0;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                return;
            }
            if (e.KeyCode == Keys.Enter)
            {
                Finish(new Rectangle(0, 0, _screen.Width, _screen.Height));
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Close();
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _hasSelection = false;
                _start = e.Location;
                _current = e.Location;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                _current = e.Location;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _dragging)
            {
                _dragging = false;
                _current = e.Location;
                Rectangle rect = SelectionRect();
                if (rect.Width >= 8 && rect.Height >= 8)
                {
                    Finish(rect);
                }
                else
                {
                    _hasSelection = false;
                    Invalidate();
                }
            }
            base.OnMouseUp(e);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            Finish(new Rectangle(0, 0, _screen.Width, _screen.Height));
            base.OnDoubleClick(e);
        }

        private Rectangle SelectionRect()
        {
            int left = Math.Min(_start.X, _current.X);
            int top = Math.Min(_start.Y, _current.Y);
            int width = Math.Abs(_current.X - _start.X);
            int height = Math.Abs(_current.Y - _start.Y);
            var rect = new Rectangle(left, top, width, height);
            return Rectangle.Intersect(rect, new Rectangle(0, 0, _screen.Width, _screen.Height));
        }

        private void Finish(Rectangle rect)
        {
            if (rect.Width < 8 || rect.Height < 8) return;
            _hasSelection = true;
            var shot = new Bitmap(rect.Width, rect.Height);
            using (var g = Graphics.FromImage(shot))
            {
                g.DrawImage(_screen, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
            }
            Result = shot;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(_screen, new Rectangle(0, 0, _screen.Width, _screen.Height), 0, 0, _screen.Width, _screen.Height, GraphicsUnit.Pixel);

            using (var dim = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
            {
                g.FillRectangle(dim, ClientRectangle);
            }

            Rectangle selection = SelectionRect();
            bool showSelection = (_dragging || _hasSelection) && selection.Width > 2 && selection.Height > 2;
            if (showSelection)
            {
                g.DrawImage(_screen, selection, selection, GraphicsUnit.Pixel);
                using (var border = new Pen(Theme.Accent, 1.6f))
                {
                    g.DrawRectangle(border, selection.Left, selection.Top, selection.Width - 1, selection.Height - 1);
                }
                DrawCorner(g, selection.Left, selection.Top, 1, 1);
                DrawCorner(g, selection.Right - 1, selection.Top, -1, 1);
                DrawCorner(g, selection.Left, selection.Bottom - 1, 1, -1);
                DrawCorner(g, selection.Right - 1, selection.Bottom - 1, -1, -1);
                DrawSizeLabel(g, selection);
            }

            DrawHint(g);
        }

        private static void DrawCorner(Graphics g, int x, int y, int dx, int dy)
        {
            using (var pen = new Pen(Color.White, 3f))
            {
                g.DrawLine(pen, x, y, x + dx * 14, y);
                g.DrawLine(pen, x, y, x, y + dy * 14);
            }
        }

        private void DrawSizeLabel(Graphics g, Rectangle selection)
        {
            string text = selection.Width + " × " + selection.Height;
            using (var font = new Font("Microsoft YaHei UI", 9F))
            using (var brush = new SolidBrush(Color.White))
            {
                SizeF size = g.MeasureString(text, font);
                float width = size.Width + 16;
                float height = size.Height + 8;
                float left = Math.Min(selection.Right - width, ClientRectangle.Right - width - 8);
                float top = selection.Bottom + 8;
                if (top + height > ClientRectangle.Bottom - 8) top = selection.Top - height - 8;
                left = Math.Max(8, left);
                top = Math.Max(8, top);
                var box = new RectangleF(left, top, width, height);
                using (var path = Theme.RoundRect(Rectangle.Round(box), 6))
                using (var fill = new SolidBrush(Color.FromArgb(230, 20, 22, 28)))
                {
                    g.FillPath(fill, path);
                }
                g.DrawString(text, font, brush, left + 8, top + 4);
            }
        }

        private void DrawHint(Graphics g)
        {
            string text = "拖拽框选要问的区域　·　Esc 取消　·　回车选全屏";
            using (var font = new Font("Microsoft YaHei UI", 10F))
            using (var brush = new SolidBrush(Color.FromArgb(235, 240, 243, 250)))
            {
                SizeF size = g.MeasureString(text, font);
                float width = size.Width + 32;
                float height = size.Height + 16;
                float left = (ClientRectangle.Width - width) / 2f;
                float top = Math.Max(24f, ClientRectangle.Top + 40f);
                var box = new RectangleF(left, top, width, height);
                using (var path = Theme.RoundRect(Rectangle.Round(box), 10))
                using (var fill = new SolidBrush(Color.FromArgb(210, 20, 22, 28)))
                using (var pen = new Pen(Color.FromArgb(80, 255, 255, 255)))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }
                g.DrawString(text, font, brush, left + 16, top + 8);
            }
        }
    }
}
