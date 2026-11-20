using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ShotAsk
{
    internal sealed class AppConfig
    {
        /// <summary>DSH 本地 Web 服务地址；端口不同时只改这一项。</summary>
        public string BaseUrl = "http://127.0.0.1:3080";
        public string Hotkey = "Ctrl+Alt+Q";
        public int Width = 430;
        public int Height = 660;
        public int X = -1;
        public int Y = -1;
        public bool Collapsed = false;
        public int MaxImageSide = 1600;
        public int JpegQuality = 85;
        public int HistoryDepth = 6;
        public string QuickQuestions = "解释|总结|翻译|出题";

        public string Endpoint { get { return BaseUrl.TrimEnd('/') + "/shot-ask/ask"; } }

        public string PingEndpoint { get { return BaseUrl.TrimEnd('/') + "/shot-ask/ping"; } }

        private static string FilePath
        {
            get
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(dir, "config.json");
            }
        }

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new AppConfig();
                string text = File.ReadAllText(FilePath, Encoding.UTF8);
                var serializer = new JavaScriptSerializer();
                var raw = serializer.Deserialize<Dictionary<string, object>>(text);
                var config = new AppConfig();
                if (raw == null) return config;
                config.BaseUrl = Str(raw, "BaseUrl", config.BaseUrl);
                config.Hotkey = Str(raw, "Hotkey", config.Hotkey);
                config.Width = Int(raw, "Width", config.Width);
                config.Height = Int(raw, "Height", config.Height);
                config.X = Int(raw, "X", config.X);
                config.Y = Int(raw, "Y", config.Y);
                config.Collapsed = Bool(raw, "Collapsed", config.Collapsed);
                config.MaxImageSide = Int(raw, "MaxImageSide", config.MaxImageSide);
                config.JpegQuality = Int(raw, "JpegQuality", config.JpegQuality);
                config.HistoryDepth = Int(raw, "HistoryDepth", config.HistoryDepth);
                config.QuickQuestions = Str(raw, "QuickQuestions", config.QuickQuestions);
                config.Clamp();
                return config;
            }
            catch (Exception)
            {
                return new AppConfig();
            }
        }

        public void Save()
        {
            try
            {
                Clamp();
                var serializer = new JavaScriptSerializer();
                var raw = new Dictionary<string, object>();
                raw["BaseUrl"] = BaseUrl;
                raw["Hotkey"] = Hotkey;
                raw["Width"] = Width;
                raw["Height"] = Height;
                raw["X"] = X;
                raw["Y"] = Y;
                raw["Collapsed"] = Collapsed;
                raw["MaxImageSide"] = MaxImageSide;
                raw["JpegQuality"] = JpegQuality;
                raw["HistoryDepth"] = HistoryDepth;
                raw["QuickQuestions"] = QuickQuestions;
                File.WriteAllText(FilePath, serializer.Serialize(raw), new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        private void Clamp()
        {
            if (Width < 360) Width = 360;
            if (Height < 460) Height = 460;
            if (MaxImageSide < 400) MaxImageSide = 400;
            if (MaxImageSide > 3000) MaxImageSide = 3000;
            if (JpegQuality < 50) JpegQuality = 50;
            if (JpegQuality > 95) JpegQuality = 95;
            if (HistoryDepth < 0) HistoryDepth = 0;
            if (HistoryDepth > 20) HistoryDepth = 20;
        }

        public Rectangle ResolveBounds(float scale = 1f)
        {
            Rectangle area = System.Windows.Forms.SystemInformation.WorkingArea;
            int w = Math.Max(360, Math.Min(Width, (int)(area.Width / scale)));
            int h = Math.Max(460, Math.Min(Height, (int)(area.Height / scale)));
            w = (int)Math.Round(w * scale);
            h = (int)Math.Round(h * scale);
            int x = X < 0 ? area.Right - w - (int)(24 * scale) : Math.Min(X, area.Right - w);
            int y = Y < 0 ? area.Top + (int)(60 * scale) : Y;
            x = Math.Max(area.Left, Math.Min(x, area.Right - w));
            y = Math.Max(area.Top, Math.Min(y, area.Bottom - (int)(60 * scale)));
            return new Rectangle(x, y, w, h);
        }

        public string[] QuickList()
        {
            return (QuickQuestions ?? string.Empty).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string Str(Dictionary<string, object> raw, string key, string fallback)
        {
            object value;
            if (!raw.TryGetValue(key, out value) || value == null) return fallback;
            string text = value as string;
            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        private static int Int(Dictionary<string, object> raw, string key, int fallback)
        {
            object value;
            if (!raw.TryGetValue(key, out value) || value == null) return fallback;
            try { return Convert.ToInt32(value); } catch (Exception) { return fallback; }
        }

        private static bool Bool(Dictionary<string, object> raw, string key, bool fallback)
        {
            object value;
            if (!raw.TryGetValue(key, out value) || value == null) return fallback;
            try { return Convert.ToBoolean(value); } catch (Exception) { return fallback; }
        }
    }
}
