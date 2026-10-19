using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace convert_LOG_for_CAT
{
    public class Program
    {
        public static void Main(string[] args)
            // Пример запуска:
            //   convert_LOG_for_CAT.exe "РАЗДЕЛ 01.txt" ["РАЗДЕЛ 02.txt" ...] [-o output.txt] [--maxrow=32] [--selftest]
        {
            if (HasFlag(args, "--selftest"))
            {
                SelfTest.Run();
                return;
            }

            List<string> inputFiles = new List<string>();
            string outputFile = null;
            int maxRow = 32;

            foreach (string a in args)
            {
                if (a.StartsWith("-o:"))
                    outputFile = a.Substring(3);
                else if (a.StartsWith("--maxrow="))
                    int.TryParse(a.Substring(9), out maxRow);
                else if (!a.StartsWith("-"))
                    inputFiles.Add(a);
            }

            if (inputFiles.Count == 0)
            {
                Console.Error.WriteLine("Использование: convert_LOG_for_CAT.exe <файл1.txt> [файл2.txt ...] [-o:output.txt] [--maxrow=32] [--selftest]");
                return;
            }

            CommandParser parser = new CommandParser();
            FormulaFormatter formatter = new FormulaFormatter();
            TranslationContext context = new TranslationContext { MaxRow = maxRow };

            StringBuilder result = new StringBuilder();

            foreach (string file in inputFiles)
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(file, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Не удалось прочитать файл " + file + ": " + ex.Message);
                    continue;
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    ParsedCommand cmd = parser.ParseLine(lines[i], i + 1);
                    if (cmd == null) continue;

                    string formula = formatter.FormatCommand(cmd, context);
                    if (string.IsNullOrEmpty(formula))
                    {
                        Console.Error.WriteLine("НЕ РАЗОБРАНО (стр. " + (i + 1) + "): " + lines[i]);
                        continue;
                    }

                    result.AppendLine(formula);
                }
            }

            if (outputFile != null)
                File.WriteAllText(outputFile, result.ToString(), Encoding.UTF8);
            else
                Console.Write(result.ToString());
        }

        private static bool HasFlag(string[] args, string flag)
        {
            foreach (string a in args)
                if (a == flag) return true;
            return false;
        }
    }
}
