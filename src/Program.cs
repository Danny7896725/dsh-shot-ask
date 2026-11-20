using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ShotAsk
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                Native.EnableDpiAwareness();

                bool created;
                using (var mutex = new Mutex(true, "ShotAsk-Single-Instance", out created))
                {
                    if (!created)
                    {
                        Log("已有实例在运行");
                        MessageBox.Show("「截图提问助手」已经在托盘中运行了。", "截图提问助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                    {
                        Log("ThreadException: " + e.Exception);
                        MessageBox.Show("程序遇到一个问题：" + e.Exception.Message, "截图提问助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    };
                    AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                    {
                        Log("UnhandledException: " + e.ExceptionObject);
                    };

                    Log("启动");
                    Application.Run(new MainForm());
                    Log("退出");
                }
            }
            catch (Exception error)
            {
                Log("Main 异常: " + error);
                throw;
            }
        }

        private static void Log(string text)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
                File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + " " + text + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception) { }
        }
    }
}
