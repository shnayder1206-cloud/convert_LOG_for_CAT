using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace convert_LOG_for_CAT
{
    /// <summary>
    /// Преобразует ParsedCommand в строку формулы вида:
    ///   1~g38>=1~$g(39-41)   || 38. ГР 38 >= сумма по графам (39-41)
    /// Исправлены ошибки:
    ///  2. при отсутствии строк у сумм/граф подставляется s(1-MaxRow);
    ///  3. диапазоны всегда оборачиваются в скобки;
    ///  4. ЕСЛИ-ТО форматируется как IF(...)(...);
    ///  5. вычитание сохраняется ("+-" между операндами).
    /// </summary>
    public class FormulaFormatter
    {
        /// <summary>
        /// Форматирует команду целиком (формула + комментарий с исходным текстом).
        /// Возвращает пустую строку, если команда не разобрана корректно.
        /// </summary>
        public string FormatCommand(ParsedCommand cmd, TranslationContext context)
        {
            if (cmd == null || context == null)
                return "";

            string formula;

            if (cmd.IsCondition)
            {
                // === ЕСЛИ ... ТО ... (Проблема 4) ===
                string condLeft = FormatSide(cmd.ConditionLeft, cmd.ConditionLeftOperators, context);
                string condRight = FormatSide(cmd.ConditionRight, cmd.ConditionRightOperators, context);
                string thenLeft = FormatSide(cmd.ThenLeft, cmd.ThenLeftOperators, context);
                string thenRight = FormatSide(cmd.ThenRight, cmd.ThenRightOperators, context);

                // Числовые операнды ("0") формально "пусты" по правилу ниже —
                // поэтому проверяем напрямую: стороны обязаны быть непустыми строками.
                if (condLeft.Length == 0 || condRight.Length == 0 ||
                    thenLeft.Length == 0 || thenRight.Length == 0 ||
                    IsEmptySide(condLeft) || IsEmptySide(thenLeft))
                {
                    return ""; // неполная команда — не выдаём битую формулу
                }

                formula = "IF(" + condLeft + cmd.ConditionOperation + condRight + ")("
                        + thenLeft + cmd.ThenOperation + thenRight + ")";
            }
            else
            {
                string left = FormatSide(cmd.Left, cmd.LeftOperators, context);
                string right = FormatSide(cmd.Right, cmd.RightOperators, context);

                if (left.Length == 0 || right.Length == 0 ||
                    IsEmptySide(left) || IsEmptySide(right) ||
                    string.IsNullOrEmpty(cmd.Operation))
                    return "";

                formula = left + cmd.Operation + right;
            }

            return formula + "   || " + FormatComment(cmd);
        }

        private static bool IsEmptySide(string side)
        {
            return string.IsNullOrEmpty(side.Replace("~", ""));
        }

        // ============================================================
        //  Сторона выражения с операторами + / - (Проблема 5)
        // ============================================================

        private string FormatSide(List<OperandNode> nodes, List<string> operators, TranslationContext context)
        {
            if (nodes == null || nodes.Count == 0)
                return "";

            StringBuilder sb = new StringBuilder();
            sb.Append(FormatNode(nodes[0], context));

            for (int i = 1; i < nodes.Count; i++)
            {
                string op = (operators != null && i - 1 < operators.Count) ? operators[i - 1] : "+";
                sb.Append(op);
                sb.Append(FormatNode(nodes[i], context));
            }

            return sb.ToString();
        }

        // ============================================================
        //  Одиночный операнд
        // ============================================================

        private string FormatNode(OperandNode node, TranslationContext context)
        {
            if (node == null || node.IsEmpty)
                return "";

            if (node.NumericValue.HasValue)
                return node.NumericValue.Value.ToString(CultureInfo.InvariantCulture);

            StringBuilder sb = new StringBuilder();

            // Префикс файла
            sb.Append(node.FileIndex);
            sb.Append("~");

            // Признак суммы
            if (node.IsSum)
                sb.Append("$");

            // === ГЛАВНОЕ: проверка на наличие строк (Проблема 2) ===
            bool hasRows = node.HasRows;
            bool hasCols = node.HasColumns;

            if (!hasRows && hasCols)
            {
                // Нет строк, но есть графы:
                //  - для сумм ($) подставляем полный диапазон s(1-MaxRow);
                //  - для одиночных графов строки не пишутся (1~g38).
                if (node.IsSum)
                {
                    sb.Append("s(1-");
                    sb.Append(context.MaxRow);
                    sb.Append(")");
                }
            }
            else if (hasRows)
            {
                sb.Append("s");
                sb.Append(FormatRows(node, context));
            }

            // Графы
            if (hasCols)
            {
                sb.Append("g");
                sb.Append(FormatColumns(node, context));
            }
            else if (!hasRows && !node.IsNumeric)
            {
                // Совсем пустой ссылочный операнд — не должно случаться, но защищаемся
                sb.Length = 0;
            }

            return sb.ToString();
        }

        // ============================================================
        //  Строки / графы (Проблема 3: диапазоны всегда в скобках)
        // ============================================================

        private string FormatRows(OperandNode node, TranslationContext context)
        {
            if (node.UsesS)
            {
                if (node.SOffset == 0)
                    return "S";
                if (node.SOffset > 0)
                    return "S+" + node.SOffset.ToString(CultureInfo.InvariantCulture);
                return "S" + node.SOffset.ToString(CultureInfo.InvariantCulture);
            }

            List<string> parts = new List<string>();

            foreach (int row in node.Rows)
                parts.Add(row.ToString(CultureInfo.InvariantCulture));

            foreach (RangeInfo range in node.RowRanges)
                parts.Add(range.Start + "-" + range.End);

            if (parts.Count == 0)
                return "";

            // одиночное число — без скобок; всё остальное (диапазоны, списки) — в скобках
            if (parts.Count == 1 && node.RowRanges.Count == 0)
                return parts[0];

            return "(" + string.Join(",", parts.ToArray()) + ")";
        }

        private string FormatColumns(OperandNode node, TranslationContext context)
        {
            if (node.UsesG)
            {
                if (node.GOffset == 0)
                    return "G";
                if (node.GOffset > 0)
                    return "G+" + node.GOffset.ToString(CultureInfo.InvariantCulture);
                return "G" + node.GOffset.ToString(CultureInfo.InvariantCulture);
            }

            List<string> parts = new List<string>();

            foreach (int col in node.Columns)
                parts.Add(col.ToString(CultureInfo.InvariantCulture));

            foreach (RangeInfo range in node.ColumnRanges)
                parts.Add(range.Start + "-" + range.End);

            if (parts.Count == 0)
                return "";

            // одиночное число — без скобок; диапазоны и списки — всегда в скобках
            if (parts.Count == 1 && node.ColumnRanges.Count == 0)
                return parts[0];

            return "(" + string.Join(",", parts.ToArray()) + ")";
        }

        // ============================================================
        //  Комментарий
        // ============================================================

        private string FormatComment(ParsedCommand cmd)
        {
            string prefix = string.IsNullOrEmpty(cmd.Prefix) ? "" : cmd.Prefix;
            string comment = prefix + cmd.Number + ". " + cmd.OriginalText;

            if (cmd.IsCommented)
                return "//" + comment;

            return comment;
        }
    }
}
