using System;
using System.Collections.Generic;

namespace convert_LOG_for_CAT
{
    /// <summary>
    /// Самопроверка парсера/форматтера на примерах из разбора ошибок РАЗДЕЛ 01.txt.
    /// </summary>
    public static class SelfTest
    {
        private class TestCase
        {
            public string Input;
            public string ExpectedFormula; // до " || "

            public TestCase(string input, string expectedFormula)
            {
                Input = input;
                ExpectedFormula = expectedFormula;
            }
        }

        public static void Run()
        {
            List<TestCase> cases = new List<TestCase>
            {
                // Проблема 1+2: сумма по графам без строк -> s(1-32), диапазоны/списки в скобках
                new TestCase("1. ГР 1 >= сумма по графам (18,43,44,45,46,48)",
                    "1~g1>=1~$s(1-32)g(18,43,44,45,46,48)"),

                // Проблема 3: диапазон обязан быть в скобках
                new TestCase("38. ГР 38 >= сумма по графам (39-41)",
                    "1~g38>=1~$s(1-32)g(39-41)"),

                // Проблема 1+2: стр S гр G
                new TestCase("5. стр S гр G >= стр S гр (G+1)",
                    "1~sSgG>=1~sSgG+1"),

                // Проблема 7: стр S гр 31 = 0
                new TestCase("7. стр S гр 31 = 0",
                    "1~sSg31=0"),

                // Проблема 4: ЕСЛИ ... ТО ...
                new TestCase("9. ЕСЛИ ГР 17 > 0 ТО ГР 50 > 0",
                    "IF(1~g17>0)(1~g50>0)"),

                // Проблема 5: вычитание не теряется
                new TestCase("12. ГР 18 = ГР 19 + ГР 20 + ГР 21 + ГР 38 - ГР 23",
                    "1~g18=1~g19+1~g20+1~g21+1~g38-1~g23"),

                // Пример 9 из таблицы: всё ок
                new TestCase("15. стр 6 гр 9 = 0",
                    "1~s6g9=0"),

                // Явные строки + список граф
                new TestCase("2. стр 1-32 гр (43-45) >= сумма по графам (49,50)",
                    "1~s(1-32)g(43-45)>=1~$s(1-32)g(49,50)"),

                // Префикс файла 2~
                new TestCase("3. 2~ГР 5 = 1~ГР 7",
                    "2~g5=1~g7"),
            };

            CommandParser parser = new CommandParser();
            FormulaFormatter formatter = new FormulaFormatter();
            TranslationContext context = new TranslationContext { MaxRow = 32 };

            int passed = 0;
            foreach (TestCase tc in cases)
            {
                ParsedCommand cmd = parser.ParseLine(tc.Input, 0);
                string full = cmd != null ? formatter.FormatCommand(cmd, context) : "";
                string formula = full.Contains(" || ") ? full.Substring(0, full.IndexOf(" || ")) : full;

                formula = formula.Trim();
                string expected = tc.ExpectedFormula.Trim();
                bool ok = formula == expected;
                if (ok) passed++;

                Console.WriteLine((ok ? "PASS" : "FAIL") + ": " + tc.Input);
                if (!ok)
                {
                    Console.WriteLine("   ожидалось [" + Hex(expected) + "]");
                    Console.WriteLine("   получено  [" + Hex(formula) + "]");
                }
            }

            Console.WriteLine("Итого: " + passed + "/" + cases.Count);
        }

        private static string Hex(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (c < 32 || c > 126) sb.Append("<" + ((int)c).ToString("X4") + ">");
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
