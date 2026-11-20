using System;
using System.IO;
using System.Text;

namespace ShotAsk
{
    internal static class Trace
    {
        private static readonly object Gate = new object();

        public static void Log(string text)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
                lock (Gate)
                {
                    File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + " " + text + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch (Exception) { }
        }
    }
}
