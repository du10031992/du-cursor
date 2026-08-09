using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    public static class MepHvacDrawingService
    {
        private const string LayerName = "MEP_HVAC";
        private const double DefaultDuctWidth = 200;

        public static void EnsureLayer()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, 4);
                tr.Commit();
            }

            doc.Editor.WriteMessage("\nDa tao/kiem tra layer MEP_HVAC.");
        }

        public static void DrawDuctRun()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            PromptPointResult start = ed.GetPoint("\nMEPHVAC: Diem dau ong: ");
            if (start.Status != PromptStatus.OK)
            {
                return;
            }

            PromptPointOptions endOpts = new PromptPointOptions("\nMEPHVAC: Diem cuoi ong: ")
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
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, 4);
                BlockTable blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    blockTable[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite);

                var duct = new Polyline(2)
                {
                    LayerId = layerId,
                    ConstantWidth = DefaultDuctWidth
                };
                duct.AddVertexAt(0, new Point2d(start.Value.X, start.Value.Y), 0, DefaultDuctWidth, DefaultDuctWidth);
                duct.AddVertexAt(1, new Point2d(end.Value.X, end.Value.Y), 0, DefaultDuctWidth, DefaultDuctWidth);

                modelSpace.AppendEntity(duct);
                tr.AddNewlyCreatedDBObject(duct, true);
                tr.Commit();
            }

            ed.WriteMessage("\nMEPHVAC: Da ve doan onng mau.");
        }

        public static void DrawDuctElbowPlaceholder()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            PromptPointResult center = ed.GetPoint("\nMEPHVAC: Tam elbow: ");
            if (center.Status != PromptStatus.OK)
            {
                return;
            }

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, 4);
                BlockTable blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    blockTable[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite);

                var arc = new Arc(
                    center.Value,
                    300,
                    0,
                    System.Math.PI / 2)
                {
                    LayerId = layerId
                };

                modelSpace.AppendEntity(arc);
                tr.AddNewlyCreatedDBObject(arc, true);
                tr.Commit();
            }

            ed.WriteMessage("\nMEPHVAC: Da ve elbow mau (arc).");
        }
    }
}
