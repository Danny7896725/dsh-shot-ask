using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ShotAsk
{
    internal static class Markdown
    {
        public static void Render(RichTextBox box, string text)
        {
            if (box == null) return;
            box.SuspendLayout();
            box.Clear();
            if (string.IsNullOrEmpty(text))
            {
                box.ResumeLayout();
                return;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool inFence = false;
            Font answerFont = box.Font;

            foreach (string rawLine in lines)
            {
                string line = rawLine;
                string trimmed = line.TrimStart();

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    inFence = !inFence;
                    continue;
                }

                if (inFence)
                {
                    AppendRun(box, line + "\n", Theme.FontMono, Theme.Code, true);
                    continue;
                }

                if (trimmed.Length == 0)
                {
                    AppendRun(box, "\n", answerFont, Theme.Text, false);
                    continue;
                }

                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    int level = 0;
                    while (level < trimmed.Length && trimmed[level] == '#') level++;
                    string heading = trimmed.Substring(level).Trim();
                    if (heading.Length == 0) continue;
                    float size = Math.Max(9.5f, 12f - level * 0.6f);
                    using (var font = new Font(answerFont.FontFamily, size, FontStyle.Bold))
                    {
                        AppendRun(box, heading + "\n", font, Theme.Text, false);
                    }
                    continue;
                }

                if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
                {
                    string body = trimmed.Substring(2);
                    AppendRun(box, "•  ", answerFont, Theme.Accent, false);
                    AppendInline(box, body, answerFont);
                    AppendRun(box, "\n", answerFont, Theme.Text, false);
                    continue;
                }

                int numbered;
                if (TryNumbered(trimmed, out numbered))
                {
                    string body = trimmed.Substring(numbered.ToString().Length + 2);
                    AppendRun(box, numbered + ".  ", answerFont, Theme.Accent, false);
                    AppendInline(box, body, answerFont);
                    AppendRun(box, "\n", answerFont, Theme.Text, false);
                    continue;
                }

                AppendInline(box, line, answerFont);
                AppendRun(box, "\n", answerFont, Theme.Text, false);
            }

            box.SelectionStart = 0;
            box.SelectionLength = 0;
            box.ResumeLayout();
        }

        private static bool TryNumbered(string line, out int value)
        {
            value = 0;
            int index = 0;
            while (index < line.Length && char.IsDigit(line[index])) index++;
            if (index == 0 || index + 1 >= line.Length) return false;
            if (line[index] != '.' || line[index + 1] != ' ') return false;
            return int.TryParse(line.Substring(0, index), out value);
        }

        private static void AppendInline(RichTextBox box, string line, Font baseFont)
        {
            var buffer = new StringBuilder();
            int i = 0;
            while (i < line.Length)
            {
                if (line[i] == '*' && i + 1 < line.Length && line[i + 1] == '*')
                {
                    int end = line.IndexOf("**", i + 2, StringComparison.Ordinal);
                    if (end > i)
                    {
                        Flush(box, ref buffer, baseFont, Theme.Text);
                        using (var bold = new Font(baseFont, FontStyle.Bold))
                        {
                            AppendRun(box, line.Substring(i + 2, end - i - 2), bold, Theme.Text, false);
                        }
                        i = end + 2;
                        continue;
                    }
                }
                if (line[i] == '`')
                {
                    int end = line.IndexOf('`', i + 1);
                    if (end > i)
                    {
                        Flush(box, ref buffer, baseFont, Theme.Text);
                        AppendRun(box, line.Substring(i + 1, end - i - 1), Theme.FontMono, Theme.Code, true);
                        i = end + 1;
                        continue;
                    }
                }
                buffer.Append(line[i]);
                i++;
            }
            Flush(box, ref buffer, baseFont, Theme.Text);
        }

        private static void Flush(RichTextBox box, ref StringBuilder buffer, Font font, Color color)
        {
            if (buffer.Length == 0) return;
            AppendRun(box, buffer.ToString(), font, color, false);
            buffer.Length = 0;
        }

        private static void AppendRun(RichTextBox box, string text, Font font, Color color, bool codeBackground)
        {
            if (string.IsNullOrEmpty(text)) return;
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionFont = font;
            box.SelectionColor = color;
            box.SelectionBackColor = codeBackground ? Theme.PanelSoft : Theme.Panel;
            box.AppendText(text);
        }
    }
}
