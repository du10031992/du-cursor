using Autodesk.AutoCAD.DatabaseServices;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    public static class MepWaterDrawingService
    {
        public static void EnsureLayer()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                MepDrawingHelper.EnsureLayer(doc.Database, tr, "MEP_WATER", 5);
                tr.Commit();
            }

            doc.Editor.WriteMessage("\nDa tao/kiem tra layer MEP_WATER.");
        }

        /// <summary>Vẽ đa điểm — tự đặt co 90 tại góc.</summary>
        public static void DrawPipeRun() =>
            MepPipeLibraryService.DrawPipeRunWithFittings(MepPipeSystem.Water);

        /// <summary>Đặt phụ kiện (co, T, van, giảm, nút…) — ưu tiên block AMC, không có thì tự tạo.</summary>
        public static void PlaceFitting() =>
            MepPipeLibraryService.PlaceFitting(MepPipeSystem.Water);

        public static void ImportLibrary() =>
            MepPipeLibraryService.ImportAmcLibrary();
    }
}
