using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    public static class MepWaterDrawingService
    {
        private const string LayerName = "MEP_WATER";
        private const double DefaultPipeWidth = 50;

        public static void EnsureLayer()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, 5);
                tr.Commit();
            }

            doc.Editor.WriteMessage("\nDa tao/kiem tra layer MEP_WATER.");
        }

        public static void DrawPipeRun()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            PromptPointResult start = ed.GetPoint("\nHe nuoc: Diem dau ong: ");
            if (start.Status != PromptStatus.OK)
            {
                return;
            }

            var endOpts = new PromptPointOptions("\nHe nuoc: Diem cuoi ong: ")
            {
                UseBasePoint = true,
                BasePoint = start.Value
            };
            PromptPointResult end = ed.GetPoint(endOpts);
            if (end.Status != PromptStatus.OK)
            {
                return;
            }

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, 5);
                BlockTable blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    blockTable[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite);

                var pipe = new Polyline(2)
                {
                    LayerId = layerId,
                    ConstantWidth = DefaultPipeWidth
                };
                pipe.AddVertexAt(0, new Point2d(start.Value.X, start.Value.Y), 0, DefaultPipeWidth, DefaultPipeWidth);
                pipe.AddVertexAt(1, new Point2d(end.Value.X, end.Value.Y), 0, DefaultPipeWidth, DefaultPipeWidth);

                modelSpace.AppendEntity(pipe);
                tr.AddNewlyCreatedDBObject(pipe, true);
                tr.Commit();
            }

            ed.WriteMessage("\nHe nuoc: Da ve doan ong.");
        }
    }
}
