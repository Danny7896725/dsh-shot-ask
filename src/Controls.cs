using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShotAsk
{
    internal enum IconKind
    {
        Camera,
        Minus,
        Close,
        Copy,
        Send,
        Refresh,
    }

    internal sealed class IconButton : Control
    {
        private bool _hover;
        private bool _down;

        public IconKind Kind
        {
            get { return _kind; }
            set { _kind = value; Invalidate(); }
        }

        private IconKind _kind;
        public Color GlyphColor { get; set; }

        public IconButton(IconKind kind)
        {
            Kind = kind;
            GlyphColor = Theme.Subtle;
            Size = new Size(30, 30);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _down = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _down = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _down = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_hover)
            {
                using (var path = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 7))
                using (var brush = new SolidBrush(Color.FromArgb(_down ? 55 : 34, 255, 255, 255)))
                {
                    g.FillPath(brush, path);
                }
            }

            Color color = Kind == IconKind.Close && _hover ? Theme.Danger : GlyphColor;
            using (var pen = new Pen(color, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float cx = Width / 2f;
                float cy = Height / 2f;
                switch (Kind)
                {
                    case IconKind.Camera:
                        g.DrawRectangle(pen, cx - 7, cy - 4.5f, 14, 9.5f);
                        g.DrawEllipse(pen, cx - 3, cy - 1.5f, 6, 6);
                        break;
                    case IconKind.Minus:
                        g.DrawLine(pen, cx - 6, cy, cx + 6, cy);
                        break;
                    case IconKind.Close:
                        g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                        g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                        break;
                    case IconKind.Copy:
                        g.DrawRectangle(pen, cx - 6.5f, cy - 6.5f, 8.5f, 8.5f);
                        g.DrawRectangle(pen, cx - 2, cy - 2, 8.5f, 8.5f);
                        break;
                    case IconKind.Send:
                        g.DrawLine(pen, cx - 6, cy + 6, cx, cy - 6);
                        g.DrawLine(pen, cx + 6, cy + 6, cx, cy - 6);
                        g.DrawLine(pen, cx - 3.2f, cy + 1.2f, cx + 3.2f, cy + 1.2f);
                        break;
                    case IconKind.Refresh:
                        g.DrawArc(pen, cx - 6.5f, cy - 6.5f, 13, 13, 40, 280);
                        g.DrawLine(pen, cx + 4.2f, cy - 6.4f, cx + 6.6f, cy - 3.4f);
                        g.DrawLine(pen, cx + 4.2f, cy - 6.4f, cx + 1.4f, cy - 4.6f);
                        break;
                }
            }
        }
    }

    internal sealed class ChipButton : Button
    {
        private bool _hover;

        public ChipButton(string text)
        {
            Text = text;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Theme.PanelSoft;
            ForeColor = Theme.Subtle;
            Font = Theme.FontSmall;
            Cursor = Cursors.Hand;
            Height = 26;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            MouseEnter += delegate { _hover = true; Invalidate(); };
            MouseLeave += delegate { _hover = false; Invalidate(); };
        }

        public int LogicalWidth
        {
            get { return TextRenderer.MeasureText(Text, Font).Width + 22; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundRect(bounds, 13))
            using (var brush = new SolidBrush(_hover ? Theme.AccentSoft : Theme.PanelSoft))
            {
                g.FillPath(brush, path);
                if (_hover)
                {
                    using (var pen = new Pen(Color.FromArgb(90, Theme.Accent), 1f)) g.DrawPath(pen, path);
                }
            }
            TextRenderer.DrawText(g, Text, Font, bounds, _hover ? Theme.Text : Theme.Subtle,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
        }
    }

    internal sealed class ShotPanel : Control
    {
        private Image _image;
        private bool _hover;

        public ShotPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Theme.Background;
            Cursor = Cursors.Hand;
            MouseEnter += delegate { _hover = true; Invalidate(); };
            MouseLeave += delegate { _hover = false; Invalidate(); };
        }

        public Image Shot
        {
            get { return _image; }
            set
            {
                if (_image != null && !ReferenceEquals(_image, value))
                {
                    var old = _image;
                    _image = value;
                    old.Dispose();
                }
                else
                {
                    _image = value;
                }
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundRect(bounds, 10))
            using (var brush = new SolidBrush(Theme.Panel))
            {
                g.FillPath(brush, path);
                if (_image == null)
                {
                    using (var pen = new Pen(Theme.Border, 1.2f))
                    {
                        pen.DashStyle = DashStyle.Dash;
                        g.DrawPath(pen, path);
                    }
                }
                else
                {
                    using (var pen = new Pen(Color.FromArgb(_hover ? 150 : 60, Theme.Accent), 1f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            if (_image == null)
            {
                string hint = "还没截图　·　按 Ctrl+Alt+Q 截取一块区域";
                using (var brush = new SolidBrush(Theme.Muted))
                {
                    var size = g.MeasureString(hint, Theme.FontSmall);
                    g.DrawString(hint, Theme.FontSmall, brush, (Width - size.Width) / 2, (Height - size.Height) / 2);
                }
                return;
            }

            float scale = Math.Min((float)(Width - 8) / _image.Width, (float)(Height - 8) / _image.Height);
            int drawWidth = Math.Max(1, (int)(_image.Width * scale));
            int drawHeight = Math.Max(1, (int)(_image.Height * scale));
            int left = (Width - drawWidth) / 2;
            int top = (Height - drawHeight) / 2;

            using (var clipPath = Theme.RoundRect(new Rectangle(left, top, drawWidth, drawHeight), 8))
            {
                var previous = g.Clip;
                g.SetClip(clipPath);
                g.DrawImage(_image, new Rectangle(left, top, drawWidth, drawHeight));
                g.Clip = previous;
            }

            if (_hover)
            {
                using (var overlay = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                using (var clipPath = Theme.RoundRect(new Rectangle(4, 4, Width - 8, Height - 8), 8))
                {
                    g.FillPath(overlay, clipPath);
                }
                string tip = "点击重新截图";
                var size = g.MeasureString(tip, Theme.FontSmall);
                using (var fill = new SolidBrush(Color.FromArgb(190, 20, 22, 28)))
                using (var path = Theme.RoundRect(Rectangle.Round(new RectangleF((Width - size.Width - 20) / 2, (Height - size.Height - 10) / 2, size.Width + 20, size.Height + 10)), 8))
                {
                    g.FillPath(fill, path);
                }
                using (var brush = new SolidBrush(Theme.Text))
                {
                    g.DrawString(tip, Theme.FontSmall, brush, (Width - size.Width) / 2, (Height - size.Height) / 2 - 2);
                }
            }
        }
    }
}
