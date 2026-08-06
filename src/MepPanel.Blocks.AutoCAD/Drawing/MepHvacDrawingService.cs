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

        /// <summary>Ve ong thang; neu chon diem uon thu 3 se ve ong chu L kem elbow.</summary>
        public static void DrawDuctRun()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            PromptPointResult start = ed.GetPoint("\nMEPHVAC: Diem dau ong: ");
            if (start.Status != PromptStatus.OK)
            {
                return;
            }

            var endOpts = new PromptPointOptions("\nMEPHVAC: Diem cuoi doan 1: ")
            {
                UseBasePoint = true,
                BasePoint = start.Value
            };
            PromptPointResult end = ed.GetPoint(endOpts);
            if (end.Status != PromptStatus.OK)
            {
                return;
            }

            var bendOpts = new PromptPointOptions("\nMEPHVAC: Diem cuoi doan 2 (Enter = chi ve doan thang): ")
            {
                UseBasePoint = true,
                BasePoint = end.Value,
                AllowNone = true
            };
            PromptPointResult bend = ed.GetPoint(bendOpts);

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, 4);
                BlockTable blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    blockTable[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite);

                if (bend.Status == PromptStatus.OK)
                {
                    var duct = new Polyline(3)
                    {
                        LayerId = layerId,
                        ConstantWidth = DefaultDuctWidth
                    };
                    duct.AddVertexAt(0, To2d(start.Value), 0, DefaultDuctWidth, DefaultDuctWidth);
                    duct.AddVertexAt(1, To2d(end.Value), 0, DefaultDuctWidth, DefaultDuctWidth);
                    duct.AddVertexAt(2, To2d(bend.Value), 0, DefaultDuctWidth, DefaultDuctWidth);
                    modelSpace.AppendEntity(duct);
                    tr.AddNewlyCreatedDBObject(duct, true);

                    AddElbowArc(modelSpace, tr, layerId, end.Value, start.Value, bend.Value);
                    ed.WriteMessage("\nMEPHVAC: Da ve ong chu L + elbow.");
                }
                else
                {
                    var duct = new Polyline(2)
                    {
                        LayerId = layerId,
                        ConstantWidth = DefaultDuctWidth
                    };
                    duct.AddVertexAt(0, To2d(start.Value), 0, DefaultDuctWidth, DefaultDuctWidth);
                    duct.AddVertexAt(1, To2d(end.Value), 0, DefaultDuctWidth, DefaultDuctWidth);
                    modelSpace.AppendEntity(duct);
                    tr.AddNewlyCreatedDBObject(duct, true);
                    ed.WriteMessage("\nMEPHVAC: Da ve doan ong thang.");
                }

                tr.Commit();
            }
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

                var arc = new Arc(center.Value, DefaultDuctWidth * 0.75, 0, System.Math.PI / 2)
                {
                    LayerId = layerId
                };

                modelSpace.AppendEntity(arc);
                tr.AddNewlyCreatedDBObject(arc, true);
                tr.Commit();
            }

            ed.WriteMessage("\nMEPHVAC: Da ve elbow mau (arc).");
        }

        private static void AddElbowArc(
            BlockTableRecord modelSpace,
            Transaction tr,
            ObjectId layerId,
            Point3d corner,
            Point3d from,
            Point3d to)
        {
            Vector3d v1 = from - corner;
            Vector3d v2 = to - corner;
            if (v1.Length < 1e-6 || v2.Length < 1e-6)
            {
                return;
            }

            double radius = DefaultDuctWidth * 0.75;
            double angle1 = System.Math.Atan2(v1.Y, v1.X);
            double angle2 = System.Math.Atan2(v2.Y, v2.X);

            var arc = new Arc(corner, radius, angle1, angle2) { LayerId = layerId };
            modelSpace.AppendEntity(arc);
            tr.AddNewlyCreatedDBObject(arc, true);
        }

        private static Point2d To2d(Point3d p) => new Point2d(p.X, p.Y);
    }
}
