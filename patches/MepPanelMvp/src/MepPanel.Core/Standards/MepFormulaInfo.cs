namespace MepPanel.Core.Standards
{
    /// <summary>Công thức tính toán khoa học — kèm đơn vị SI.</summary>
    public sealed class MepFormulaInfo
    {
        public MepFormulaInfo(
            string id,
            string nameVi,
            string expression,
            string variablesVi,
            string unitNote,
            string standardRef)
        {
            Id = id;
            NameVi = nameVi;
            Expression = expression;
            VariablesVi = variablesVi;
            UnitNote = unitNote;
            StandardRef = standardRef;
        }

        public string Id { get; }
        public string NameVi { get; }
        public string Expression { get; }
        public string VariablesVi { get; }
        public string UnitNote { get; }
        public string StandardRef { get; }
    }
}
