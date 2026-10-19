using System.Collections.Generic;

namespace convert_LOG_for_CAT
{
    /// <summary>
    /// Диапазон строк или граф (например 39-41).
    /// </summary>
    public class RangeInfo
    {
        public int Start { get; set; }
        public int End { get; set; }

        public RangeInfo() { }

        public RangeInfo(int start, int end)
        {
            Start = start;
            End = end;
        }
    }

    /// <summary>
    /// Операнд команды: ссылка на строки/графы таблицы (возможно с признаком суммы),
    /// либо числовое значение. Поддерживаются относительные ссылки S / G (+/- смещение).
    /// </summary>
    public class OperandNode
    {
        // === Ссылочный операнд ===
        public int FileIndex { get; set; }          // префикс файла: 1~ , 2~ (по умолчанию 1)
        public bool IsSum { get; set; }             // признак "$" (сумма по графам/строкам)

        public List<int> Rows { get; set; }         // явные номера строк
        public List<RangeInfo> RowRanges { get; set; }
        public bool UsesS { get; set; }             // "стр S", "S+1", "S-2"
        public int SOffset { get; set; }

        public List<int> Columns { get; set; }      // явные номера граф
        public List<RangeInfo> ColumnRanges { get; set; }
        public bool UsesG { get; set; }             // "гр G", "G+1", "G-1"
        public int GOffset { get; set; }

        // === Числовой операнд ===
        public bool IsNumeric { get; set; }
        public double? NumericValue { get; set; }

        // true, если вообще не удалось разобрать операнд (защита от пустых формул)
        public bool IsEmpty { get; set; }

        public OperandNode()
        {
            FileIndex = 1;
            IsSum = false;
            Rows = new List<int>();
            RowRanges = new List<RangeInfo>();
            UsesS = false;
            SOffset = 0;
            Columns = new List<int>();
            ColumnRanges = new List<RangeInfo>();
            UsesG = false;
            GOffset = 0;
            IsNumeric = false;
            NumericValue = null;
            IsEmpty = false;
        }

        /// <summary>Есть ли у операнда указание на строки (явное или через S).</summary>
        public bool HasRows
        {
            get { return Rows.Count > 0 || RowRanges.Count > 0 || UsesS; }
        }

        /// <summary>Есть ли у операнда указание на графы (явное или через G).</summary>
        public bool HasColumns
        {
            get { return Columns.Count > 0 || ColumnRanges.Count > 0 || UsesG; }
        }
    }

    /// <summary>
    /// Разобранная команда проверочной логики.
    /// </summary>
    public class ParsedCommand
    {
        public int Number { get; set; }             // номер команды ("9." -> 9)
        public string Prefix { get; set; }          // "П" или ""

        // Обычная команда: Left <Operation> Right
        public List<OperandNode> Left { get; set; }
        public string Operation { get; set; }
        public List<OperandNode> Right { get; set; }

        // Операторы между операндами одной стороны ("+" / "-")
        public List<string> LeftOperators { get; set; }
        public List<string> RightOperators { get; set; }

        // ЕСЛИ ... ТО ...
        public bool IsCondition { get; set; }
        public List<OperandNode> ConditionLeft { get; set; }
        public string ConditionOperation { get; set; }
        public List<OperandNode> ConditionRight { get; set; }
        public List<string> ConditionLeftOperators { get; set; }
        public List<string> ConditionRightOperators { get; set; }

        public List<OperandNode> ThenLeft { get; set; }
        public string ThenOperation { get; set; }
        public List<OperandNode> ThenRight { get; set; }
        public List<string> ThenLeftOperators { get; set; }
        public List<string> ThenRightOperators { get; set; }

        public string OriginalText { get; set; }
        public int LineNumber { get; set; }
        public bool IsCommented { get; set; }

        public ParsedCommand()
        {
            Number = 0;
            Prefix = "";
            Left = new List<OperandNode>();
            Operation = "";
            Right = new List<OperandNode>();
            LeftOperators = new List<string>();
            RightOperators = new List<string>();

            IsCondition = false;
            ConditionLeft = new List<OperandNode>();
            ConditionOperation = "";
            ConditionRight = new List<OperandNode>();
            ConditionLeftOperators = new List<string>();
            ConditionRightOperators = new List<string>();

            ThenLeft = new List<OperandNode>();
            ThenOperation = "";
            ThenRight = new List<OperandNode>();
            ThenLeftOperators = new List<string>();
            ThenRightOperators = new List<string>();

            OriginalText = "";
            LineNumber = 0;
            IsCommented = false;
        }
    }

    /// <summary>
    /// Контекст перевода: параметры таблицы, влияющие на формат формулы.
    /// </summary>
    public class TranslationContext
    {
        public int MaxRow { get; set; }     // максимальный номер строки таблицы (для s(1-MaxRow))
        public int MaxColumn { get; set; }  // максимальный номер графы

        public TranslationContext()
        {
            MaxRow = 32;
            MaxColumn = 50;
        }
    }
}
