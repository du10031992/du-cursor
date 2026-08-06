using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    public static class MepCabinetDrawingService
    {
        private const string LayerName = "MEPDB_CABINET";

        public static void DrawCabinet2D()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            PromptPointResult corner1 = ed.GetPoint("\nVe tu dien: Goc duoi-trai tu: ");
            if (corner1.Status != PromptStatus.OK)
            {
                return;
            }

            var cornerOpts = new PromptCornerOptions("\nVe tu dien: Goc tren-phai tu: ", corner1.Value);
            PromptPointResult corner2 = ed.GetCorner(cornerOpts);
            if (corner2.Status != PromptStatus.OK)
            {
                return;
            }

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, 3);
                BlockTable blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    blockTable[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite);

                double x1 = corner1.Value.X;
                double y1 = corner1.Value.Y;
                double x2 = corner2.Value.X;
                double y2 = corner2.Value.Y;

                var outline = CreateRectPolyline(layerId, x1, y1, x2, y2);
                modelSpace.AppendEntity(outline);
                tr.AddNewlyCreatedDBObject(outline, true);

                // 3 ngăn tủ mẫu
                double width = System.Math.Abs(x2 - x1);
                double height = System.Math.Abs(y2 - y1);
                double cellW = width / 3;
                double minX = System.Math.Min(x1, x2);
                double minY = System.Math.Min(y1, y2);
                double maxY = System.Math.Max(y1, y2);

                for (int i = 1; i < 3; i++)
                {
                    double x = minX + cellW * i;
                    var divider = new Line(new Point3d(x, minY, 0), new Point3d(x, maxY, 0))
                    {
                        LayerId = layerId
                    };
                    modelSpace.AppendEntity(divider);
                    tr.AddNewlyCreatedDBObject(divider, true);
                }

                tr.Commit();
            }

            ed.WriteMessage("\nVe tu dien: Da ve tu 2D (3 ngăn mẫu).");
        }

        public static void UpdateCabinet()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            var opts = new PromptEntityOptions("\nCap nhat tu: Chon block tu dien: ")
            {
                AllowNone = false
            };
            opts.SetRejectMessage("\nChi chon BlockReference.");
            opts.AddAllowedClass(typeof(BlockReference), true);

            PromptEntityResult pick = ed.GetEntity(opts);
            if (pick.Status != PromptStatus.OK)
            {
                return;
            }

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                var blockRef = tr.GetObject(pick.ObjectId, OpenMode.ForWrite) as BlockReference;
                if (blockRef == null)
                {
                    ed.WriteMessage("\nCap nhat tu: Khong doc duoc block.");
                    return;
                }

                blockRef.RecordGraphicsModified(true);
                tr.Commit();
            }

            ed.WriteMessage("\nCap nhat tu: Da danh dau cap nhat block (stub — thuoc tinh day du trong ban WPF).");
        }

        public static void ExportExcelHint()
        {
            MepDrawingHelper.GetActiveDocument().Editor.WriteMessage(
                "\nXuat Excel: Chuc nang day du co trong plugin release (PanelExcelExporter).");
        }

        public static void Draw3P4DHint()
        {
            MepDrawingHelper.GetActiveDocument().Editor.WriteMessage(
                "\nSo do 3P-4D+E: Chuc nang day du co trong plugin release (Diagram3P4DWindow).");
        }

        public static void DrawPowerLayout()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            PromptPointResult start = ed.GetPoint("\nBo tri dong luc: Diem bat dau day: ");
            if (start.Status != PromptStatus.OK)
            {
                return;
            }

            var endOpts = new PromptPointOptions("\nBo tri dong luc: Diem ket thuc day: ")
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
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, 3);
                BlockTable blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    blockTable[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite);

                var wire = new Line(start.Value, end.Value) { LayerId = layerId };
                modelSpace.AppendEntity(wire);
                tr.AddNewlyCreatedDBObject(wire, true);
                tr.Commit();
            }

            ed.WriteMessage("\nBo tri dong luc: Da ve day mau.");
        }

        private static Polyline CreateRectPolyline(ObjectId layerId, double x1, double y1, double x2, double y2)
        {
            var rect = new Polyline(4) { LayerId = layerId, Closed = true };
            rect.AddVertexAt(0, new Point2d(x1, y1), 0, 0, 0);
            rect.AddVertexAt(1, new Point2d(x2, y1), 0, 0, 0);
            rect.AddVertexAt(2, new Point2d(x2, y2), 0, 0, 0);
            rect.AddVertexAt(3, new Point2d(x1, y2), 0, 0, 0);
            return rect;
        }
    }
}
