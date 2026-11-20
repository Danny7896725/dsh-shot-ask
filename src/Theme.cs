using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShotAsk
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(0x17, 0x18, 0x1C);
        public static readonly Color Panel = Color.FromArgb(0x1F, 0x21, 0x26);
        public static readonly Color PanelSoft = Color.FromArgb(0x26, 0x29, 0x2F);
        public static readonly Color Border = Color.FromArgb(0x32, 0x36, 0x3E);
        public static readonly Color Text = Color.FromArgb(0xE8, 0xEA, 0xEF);
        public static readonly Color Subtle = Color.FromArgb(0x9A, 0xA1, 0xB2);
        public static readonly Color Muted = Color.FromArgb(0x6B, 0x72, 0x82);
        public static readonly Color Accent = Color.FromArgb(0x5B, 0x8C, 0xFF);
        public static readonly Color AccentSoft = Color.FromArgb(0x2A, 0x36, 0x55);
        public static readonly Color Danger = Color.FromArgb(0xF2, 0x6D, 0x6D);
        public static readonly Color Code = Color.FromArgb(0x9C, 0xD8, 0xFF);

        public static readonly Font FontUi = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FontSmall = new Font("Microsoft YaHei UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FontTitle = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font FontAnswer = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FontMono = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);

        public static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || bounds.Width < d || bounds.Height < d)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
