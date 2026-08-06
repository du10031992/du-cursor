namespace MepPanel.Core.Calculations
{
    public sealed class MepCalculationResult
    {
        public MepCalculationResult(string title, string formulaId, double value, string unit, string note)
        {
            Title = title;
            FormulaId = formulaId;
            Value = value;
            Unit = unit;
            Note = note;
        }

        public string Title { get; }
        public string FormulaId { get; }
        public double Value { get; }
        public string Unit { get; }
        public string Note { get; }

        public override string ToString() =>
            $"{Title}: {Value:0.####} {Unit} ({Note})";
    }
}
