using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace convert_LOG_for_CAT
{
    /// <summary>
    /// Разбирает тексты команд проверочной логики (РАЗДЕЛ NN.txt) в ParsedCommand.
    /// Исправлены ошибки:
    ///  1. S и G теперь разбираются (в т.ч. в комбинации "стр S гр G");
    ///  4. ЕСЛИ ... ТО ... распознаётся как условие;
    ///  5. вычитание "-" сохраняется между операндами.
    /// </summary>
    public class CommandParser
    {
        // Номера команд: "9." или "П12." в начале строки
        private static readonly Regex PrefixRegex =
            new Regex(@"^(?<num>П?\d+)\.\s*(?<rest>.*)$", RegexOptions.IgnoreCase);

        // ЕСЛИ <условие> ТО <действие>
        private static readonly Regex IfRegex =
            new Regex(@"^ЕСЛИ\s+(?<cond>.+?)\s+ТО\s+(?<then>.+)$",
                      RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Операторы сравнения (длинные — первыми)
        private static readonly string[] ComparisonOperators = { ">=", "<=", "<>", "=", ">", "<" };

        /// <summary>
        /// Разбор одной команды. prefixNum — "9" или "П3", expression — текст после номера.
        /// </summary>
        public ParsedCommand Parse(string prefixNum, string expression, bool isCommented, int lineNumber)
        {
            ParsedCommand cmd = new ParsedCommand();
            cmd.OriginalText = expression;
            cmd.LineNumber = lineNumber;
            cmd.IsCommented = isCommented;

            if (prefixNum.StartsWith("П"))
            {
                cmd.Prefix = "П";
                cmd.Number = int.Parse(prefixNum.Substring(1));
            }
            else
            {
                cmd.Number = int.Parse(prefixNum);
            }

            // === ПРОВЕРКА НА ЕСЛИ-ТО (Проблема 4) ===
            Match ifMatch = IfRegex.Match(expression.Trim());
            if (ifMatch.Success)
            {
                cmd.IsCondition = true;

                string conditionPart = Clean(ifMatch.Groups["cond"].Value);
                string thenPart = Clean(ifMatch.Groups["then"].Value);

                string condOp;
                List<OperandNode> condLeft;
                List<string> condLeftOps;
                List<OperandNode> condRight;
                List<string> condRightOps;
                ParseBinaryExpression(conditionPart, out condLeft,
                                      out condLeftOps,
                                      out condOp,
                                      out condRight,
                                      out condRightOps);
                cmd.ConditionOperation = condOp;
                cmd.ConditionLeft = condLeft;
                cmd.ConditionLeftOperators = condLeftOps;
                cmd.ConditionRight = condRight;
                cmd.ConditionRightOperators = condRightOps;

                string thenOp;
                List<OperandNode> thenLeft;
                List<string> thenLeftOps;
                List<OperandNode> thenRight;
                List<string> thenRightOps;
                ParseBinaryExpression(thenPart, out thenLeft,
                                     out thenLeftOps,
                                     out thenOp,
                                     out thenRight,
                                     out thenRightOps);
                cmd.ThenOperation = thenOp;
                cmd.ThenLeft = thenLeft;
                cmd.ThenLeftOperators = thenLeftOps;
                cmd.ThenRight = thenRight;
                cmd.ThenRightOperators = thenRightOps;

                return cmd;
            }

            // Обычная команда
            string op;
            List<OperandNode> left;
            List<string> leftOps;
            List<OperandNode> right;
            List<string> rightOps;
            ParseBinaryExpression(Clean(expression), out left, out leftOps,
                                  out op, out right, out rightOps);
            cmd.Operation = op;
            cmd.Left = left;
            cmd.LeftOperators = leftOps;
            cmd.Right = right;
            cmd.RightOperators = rightOps;

            return cmd;
        }

        /// <summary>
        /// Разбор строки файла целиком ("9. ГР 17 > 0"). Возвращает null, если строка не команда.
        /// </summary>
        public ParsedCommand ParseLine(string line, int lineNumber)
        {
            if (line == null) return null;

            string text = line.Replace("\t", " ").Trim();
            if (text.Length == 0) return null;

            bool commented = false;
            if (text.StartsWith("//") || text.StartsWith("*"))
            {
                commented = true;
                text = text.Substring(2).Trim();
            }

            Match m = PrefixRegex.Match(text);
            if (!m.Success) return null;

            return Parse(m.Groups["num"].Value, m.Groups["rest"].Value, commented, lineNumber);
        }

        // ============================================================
        //  Бинарное выражение: левая сторона OP правая сторона
        // ============================================================

        private void ParseBinaryExpression(string text,
                                           out List<OperandNode> left,
                                           out List<string> leftOps,
                                           out string operation,
                                           out List<OperandNode> right,
                                           out List<string> rightOps)
        {
            left = new List<OperandNode>();
            leftOps = new List<string>();
            right = new List<OperandNode>();
            rightOps = new List<string>();
            operation = "";

            string op = FindComparisonOperator(text, out int idx);
            if (op == null)
            {
                // Оператора сравнения нет — считаем всё одной стороной
                left = ParseSide(text, out leftOps);
                return;
            }

            operation = op;
            string leftText = text.Substring(0, idx).Trim();
            string rightText = text.Substring(idx + op.Length).Trim();

            left = ParseSide(leftText, out leftOps);
            right = ParseSide(rightText, out rightOps);
        }

        /// <summary>
        /// Ищет оператор сравнения на верхнем уровне (вне скобок).
        /// </summary>
        private string FindComparisonOperator(string text, out int index)
        {
            index = -1;
            int depth = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (depth == 0)
                {
                    foreach (string op in ComparisonOperators)
                    {
                        if (i + op.Length <= text.Length &&
                            string.CompareOrdinal(text.Substring(i, op.Length), op) == 0)
                        {
                            // "=" внутри "<=" / ">=" уже обработан порядком списка
                            index = i;
                            return op;
                        }
                    }
                }
            }

            return null;
        }

        // ============================================================
        //  Сторона выражения: операнды, разделённые + и - (Проблема 5)
        // ============================================================

        /// <summary>
        /// Разделяет текст стороны по "+" и "-" на верхнем уровне, сохраняя операторы.
        /// Дефис внутри слова ("сумма по графам", "П-образная") не считается оператором —
        /// оператором "-" является только дефис, ограниченный пробелами.
        /// </summary>
        private List<OperandNode> ParseSide(string text, out List<string> operators)
        {
            List<OperandNode> result = new List<OperandNode>();
            operators = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
                return result;

            List<string> tokens = new List<string>();
            StringBuilder current = new StringBuilder();
            int depth = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '(') { depth++; current.Append(c); continue; }
                if (c == ')') { depth--; current.Append(c); continue; }

                bool isOperator = false;
                if (depth == 0)
                {
                    if (c == '+')
                    {
                        isOperator = true;
                    }
                    else if (c == '-')
                    {
                        // "-" как оператор: окружён пробелами (или стоит в начале токена)
                        bool leftSpace = (current.Length == 0) || IsSpaceBefore(current);
                        bool rightSpace = (i + 1 < text.Length) && (text[i + 1] == ' ' || text[i + 1] == '\t');
                        isOperator = leftSpace && rightSpace;
                    }
                }

                if (isOperator)
                {
                    string token = current.ToString().Trim();
                    if (token.Length > 0) tokens.Add(token);
                    operators.Add(c.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            string last = current.ToString().Trim();
            if (last.Length > 0) tokens.Add(last);

            foreach (string token in tokens)
            {
                OperandNode node = ParseOperand(token);
                if (node != null)
                    result.Add(node);
            }

            // Если токенов меньше, чем операторов + 1 — обрезаем операторы
            while (operators.Count > Math.Max(0, result.Count - 1))
                operators.RemoveAt(operators.Count - 1);

            return result;
        }

        private static bool IsSpaceBefore(StringBuilder sb)
        {
            return sb.Length > 0 && (sb[sb.Length - 1] == ' ' || sb[sb.Length - 1] == '\t');
        }

        // ============================================================
        //  Операнд
        // ============================================================

        /// <summary>
        /// Разбирает один операнд. Поддерживаются:
        ///   "ГР 38", "стр 5 гр 12", "стр 1-32 гр (18,43)",
        ///   "сумма по графам (39-41)", "сумма по строкам и графам (...)",
        ///   "S", "G", "S+1", "G-1", "стр S гр G", числа.
        /// </summary>
        public OperandNode ParseOperand(string text)
        {
            OperandNode node = new OperandNode();
            if (string.IsNullOrWhiteSpace(text))
            {
                node.IsEmpty = true;
                return node;
            }

            string s = Normalize(text);

            // --- префикс файла (1~, 2~) ---
            Match fileMatch = Regex.Match(s, @"^(?<n>\d+)~\s*");
            if (fileMatch.Success)
            {
                node.FileIndex = int.Parse(fileMatch.Groups["n"].Value);
                s = s.Substring(fileMatch.Length).Trim();
            }

            // --- признак суммы ---
            bool sumRequested = false;
            Match sumMatch = SumPrefixRegex.Match(s);
            if (sumMatch.Success)
            {
                sumRequested = true;
                node.IsSum = true;
                s = s.Substring(sumMatch.Length).Trim();
            }

            // --- число ---
            if (Regex.IsMatch(s, @"^-?\d+(\.\d+)?$"))
            {
                node.IsNumeric = true;
                node.NumericValue = double.Parse(s, CultureInfo.InvariantCulture);
                return node;
            }

            // === ПРОВЕРКА S И G (Проблема 1) ===

            // Одиночные "S", "G", "S+1", "G-1" (и "стр S", "гр G")
            Match solo = Regex.Match(s, @"^(?:стр|гр)?\s*(?<key>[SG])(?<off>[+-]\d+)?$", RegexOptions.IgnoreCase);
            if (solo.Success)
            {
                ApplyKey(node, solo.Groups["key"].Value.ToUpperInvariant(), solo.Groups["off"].Value);
                if (sumRequested) FinalizeSumScope(node, s);
                return node;
            }

            // Комбинированные ссылки: "стр <rows> [гр <cols>]" или "гр <cols>"
            Match rowCol = RowColRegex.Match(s);
            if (rowCol.Success)
            {
                string rowsText = null;
                string colsText = null;

                if (rowCol.Groups["onlycols"].Success)
                {
                    colsText = UnwrapParens(rowCol.Groups["onlycols"].Value);
                }
                else
                {
                    rowsText = UnwrapParens(rowCol.Groups["rows"].Value);
                    if (rowCol.Groups["cols"].Success)
                        colsText = UnwrapParens(rowCol.Groups["cols"].Value);
                }

                if (rowsText != null)
                    ParseRowSpec(node, rowsText);
                if (colsText != null)
                    ParseColSpec(node, colsText);

                return node;
            }

            // Только графы без слова "гр": "(18,43)" / "39-41" — маловероятно, но допускаем
            if (Regex.IsMatch(s, @"^\(?[\d,\-\s]+\)?$"))
            {
                ParseColSpec(node, UnwrapParens(s));
                return node;
            }

            // Нераспознанный операнд
            node.IsEmpty = true;
            return node;
        }

        // "сумма по графам", "сумма по строкам", "сумма по строкам и графам", "$"
        private static readonly Regex SumPrefixRegex =
            new Regex(@"^(?:\$|СУММА\s+ПО\s+(?:СТРОКАМ\s+И\s+ГРАФАМ|ГРАФАМ|СТРОКАМ))\s*",
                      RegexOptions.IgnoreCase);

        // "стр <что-то> гр <что-то>"  /  "гр <что-то>"  /  "стр <что-то>"
        private static readonly Regex RowColRegex =
            new Regex(@"^(?:стр\s+(?<rows>.+?)(?:\s+гр\s+(?<cols>.+?))?$|гр\s+(?<onlycols>.+?)$)",
                      RegexOptions.IgnoreCase);

        private static string Normalize(string text)
        {
            string s = text.Replace('\u00A0', ' ').Replace("\t", " ").Trim();
            s = Regex.Replace(s, @"\s+", " ");
            return s;
        }

        private static string Clean(string text)
        {
            string s = text.Replace('\u00A0', ' ').Replace("\t", " ");
            s = Regex.Replace(s, @"\s+", " ");
            return s.TrimEnd('.', ',').Trim();
        }

        private static string UnwrapParens(string value)
        {
            string v = value.Trim();
            if (v.StartsWith("(") && v.EndsWith(")"))
                return v.Substring(1, v.Length - 2).Trim();
            return v;
        }

        private static void ApplyKey(OperandNode node, string key, string offsetText)
        {
            int offset = 0;
            if (!string.IsNullOrEmpty(offsetText))
                offset = int.Parse(offsetText, CultureInfo.InvariantCulture);

            if (key == "S")
            {
                node.UsesS = true;
                node.SOffset = offset;
            }
            else
            {
                node.UsesG = true;
                node.GOffset = offset;
            }
        }

        /// <summary>
        /// Разбирает спецификацию строк: "5", "1-32", "(1,2,5-9)", "S", "S+1".
        /// </summary>
        private void ParseRowSpec(OperandNode node, string spec)
        {
            foreach (string part in SplitList(spec))
            {
                Match sm = Regex.Match(part, @"^(?<key>[SG])(?<off>[+-]\d+)?$", RegexOptions.IgnoreCase);
                if (sm.Success)
                {
                    ApplyKey(node, "S", sm.Groups["off"].Value);
                    continue;
                }

                Match rm = Regex.Match(part, @"^(?<a>\d+)\s*-\s*(?<b>\d+)$");
                if (rm.Success)
                {
                    node.RowRanges.Add(new RangeInfo(int.Parse(rm.Groups["a"].Value),
                                                     int.Parse(rm.Groups["b"].Value)));
                    continue;
                }

                if (Regex.IsMatch(part, @"^\d+$"))
                {
                    node.Rows.Add(int.Parse(part));
                }
            }
        }

        /// <summary>
        /// Разбирает спецификацию граф: "38", "39-41", "(18,43,44-48)", "G", "G+1".
        /// </summary>
        private void ParseColSpec(OperandNode node, string spec)
        {
            foreach (string part in SplitList(spec))
            {
                Match sm = Regex.Match(part, @"^(?<key>[SG])(?<off>[+-]\d+)?$", RegexOptions.IgnoreCase);
                if (sm.Success)
                {
                    ApplyKey(node, "G", sm.Groups["off"].Value);
                    continue;
                }

                Match rm = Regex.Match(part, @"^(?<a>\d+)\s*-\s*(?<b>\d+)$");
                if (rm.Success)
                {
                    node.ColumnRanges.Add(new RangeInfo(int.Parse(rm.Groups["a"].Value),
                                                        int.Parse(rm.Groups["b"].Value)));
                    continue;
                }

                if (Regex.IsMatch(part, @"^\d+$"))
                {
                    node.Columns.Add(int.Parse(part));
                }
            }
        }

        private static IEnumerable<string> SplitList(string spec)
        {
            string[] parts = spec.Split(',');
            foreach (string p in parts)
            {
                string t = p.Trim();
                if (t.Length > 0)
                    yield return t;
            }
        }

        /// <summary>
        /// Для "сумма по ..." без явного диапазона строк оставляем пустой список —
        /// FormulaFormatter сам подставит s(1-MaxRow) (Проблема 2).
        /// Для "сумма по строкам" без явных графов — аналогично с g(1-MaxColumn).
        /// </summary>
        private void FinalizeSumScope(OperandNode node, string original)
        {
            bool byRows = Regex.IsMatch(original, @"ПО\s+СТРОКАМ", RegexOptions.IgnoreCase);
            if (byRows && !node.HasColumns && !node.HasRows)
            {
                // сумма по строкам: диапазон строк подставит форматтер, графы тоже
            }
        }
    }
}
