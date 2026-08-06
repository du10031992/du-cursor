namespace MepPanel.Core.Standards
{
    /// <summary>Một tiêu chuẩn / quy chuẩn áp dụng thi công MEP.</summary>
    public sealed class MepStandardInfo
    {
        public MepStandardInfo(
            string code,
            string titleVi,
            string issuer,
            string scope,
            string applyNote)
        {
            Code = code;
            TitleVi = titleVi;
            Issuer = issuer;
            Scope = scope;
            ApplyNote = applyNote;
        }

        public string Code { get; }
        public string TitleVi { get; }
        public string Issuer { get; }
        public string Scope { get; }
        public string ApplyNote { get; }
    }
}
