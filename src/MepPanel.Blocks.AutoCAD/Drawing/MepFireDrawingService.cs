namespace MepPanel.Blocks.AutoCAD.Drawing
{
    public static class MepFireDrawingService
    {
        /// <summary>Vẽ ống PCCC đa điểm — tự đặt co 90 tại góc.</summary>
        public static void DrawPipeRun() =>
            MepPipeLibraryService.DrawPipeRunWithFittings(MepPipeSystem.Fire);

        /// <summary>Đặt phụ kiện PCCC (co, T, van, sprinkler, chữa cháy, đầu báo…).</summary>
        public static void PlaceFitting() =>
            MepPipeLibraryService.PlaceFitting(MepPipeSystem.Fire);

        /// <summary>Đặt nhanh đầu báo (tương thích nút cũ).</summary>
        public static void InsertDetector() =>
            MepPipeLibraryService.PlaceFitting(MepPipeSystem.Fire, MepPipeFittingKind.Detector);

        public static void ImportLibrary() =>
            MepPipeLibraryService.ImportAmcLibrary();
    }
}
