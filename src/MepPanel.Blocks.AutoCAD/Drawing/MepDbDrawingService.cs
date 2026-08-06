using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    public static class MepDbDrawingService
    {
        private const string LayerName = "MEPDB";
        private const string BlockName = "MEPDB_EQUIP";

        public static void EnsureLayer()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, 3);
                tr.Commit();
            }

            doc.Editor.WriteMessage("\nDa tao/kiem tra layer MEPDB.");
        }

        public static void InsertEquipmentMarker()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            PromptPointResult pick = ed.GetPoint("\nMEPDB: Chon vi tri thiet bi: ");
            if (pick.Status != PromptStatus.OK)
            {
                return;
            }

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, 3);
                ObjectId blockId = MepDrawingHelper.EnsureBlockDefinition(
                    doc.Database,
                    tr,
                    BlockName,
                    blockDef =>
                    {
                        var circle = new Circle(Point3d.Origin, Vector3d.ZAxis, 250)
                        {
                            LayerId = layerId,
                            ColorIndex = 3
                        };
                        blockDef.AppendEntity(circle);
                        tr.AddNewlyCreatedDBObject(circle, true);
                    });

                BlockTable blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    blockTable[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite);

                var blockRef = new BlockReference(pick.Value, blockId)
                {
                    LayerId = layerId,
                    ScaleFactors = new Scale3d(1, 1, 1)
                };
                modelSpace.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);
                tr.Commit();
            }

            ed.WriteMessage("\nMEPDB: Da chen block MEPDB_EQUIP.");
        }

        /// <summary>Vẽ block thiết bị tủ điện (entry MEPDB trên panel).</summary>
        public static void DrawDbBlock()
        {
            EnsureLayer();
            InsertEquipmentMarker();
        }

        public static void DrawEquipmentZone()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            PromptPointResult corner1 = ed.GetPoint("\nMEPDB: Diem goc 1 vung thiet bi: ");
            if (corner1.Status != PromptStatus.OK)
            {
                return;
            }

            PromptCornerOptions cornerOpts = new PromptCornerOptions(
                "\nMEPDB: Diem goc doi dien: ",
                corner1.Value);
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

                var rect = new Polyline(4)
                {
                    LayerId = layerId,
                    Closed = true
                };
                rect.AddVertexAt(0, new Point2d(corner1.Value.X, corner1.Value.Y), 0, 0, 0);
                rect.AddVertexAt(1, new Point2d(corner2.Value.X, corner1.Value.Y), 0, 0, 0);
                rect.AddVertexAt(2, new Point2d(corner2.Value.X, corner2.Value.Y), 0, 0, 0);
                rect.AddVertexAt(3, new Point2d(corner1.Value.X, corner2.Value.Y), 0, 0, 0);

                modelSpace.AppendEntity(rect);
                tr.AddNewlyCreatedDBObject(rect, true);
                tr.Commit();
            }

            ed.WriteMessage("\nMEPDB: Da ve vung thiet bi.");
        }
    }
}
