using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    public static class MepFireDrawingService
    {
        private const string LayerName = "MEP_FIRE";
        private const string BlockName = "MEP_FIRE_DETECTOR";

        public static void EnsureLayer()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, 1);
                tr.Commit();
            }

            doc.Editor.WriteMessage("\nDa tao/kiem tra layer MEP_FIRE.");
        }

        public static void InsertDetector()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            PromptPointResult pick = ed.GetPoint("\nBao chay: Vi tri dau bao: ");
            if (pick.Status != PromptStatus.OK)
            {
                return;
            }

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, 1);
                ObjectId blockId = MepDrawingHelper.EnsureBlockDefinition(
                    doc.Database,
                    tr,
                    BlockName,
                    blockDef =>
                    {
                        var outer = new Circle(Point3d.Origin, Vector3d.ZAxis, 200)
                        {
                            LayerId = layerId,
                            ColorIndex = 1
                        };
                        var inner = new Circle(Point3d.Origin, Vector3d.ZAxis, 80)
                        {
                            LayerId = layerId,
                            ColorIndex = 1
                        };
                        blockDef.AppendEntity(outer);
                        blockDef.AppendEntity(inner);
                        tr.AddNewlyCreatedDBObject(outer, true);
                        tr.AddNewlyCreatedDBObject(inner, true);
                    });

                BlockTable blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    blockTable[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite);

                var blockRef = new BlockReference(pick.Value, blockId) { LayerId = layerId };
                modelSpace.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);
                tr.Commit();
            }

            ed.WriteMessage("\nBao chay: Da chen dau bao.");
        }
    }
}
