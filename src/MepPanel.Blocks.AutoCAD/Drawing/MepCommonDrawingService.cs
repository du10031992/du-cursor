using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    public static class MepCommonDrawingService
    {
        public static void SelectSameLayer()
        {
            Document doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            PromptEntityOptions opts = new PromptEntityOptions("\nChon cung layer: Chon doi tuong mau: ");
            PromptEntityResult pick = ed.GetEntity(opts);
            if (pick.Status != PromptStatus.OK)
            {
                return;
            }

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                Entity sample = tr.GetObject(pick.ObjectId, OpenMode.ForRead) as Entity;
                if (sample == null)
                {
                    ed.WriteMessage("\nKhong doc duoc doi tuong mau.");
                    return;
                }

                string layerName = sample.Layer;
                ObjectId layerId = sample.LayerId;
                BlockTable blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    blockTable[BlockTableRecord.ModelSpace],
                    OpenMode.ForRead);

                int count = 0;
                foreach (ObjectId id in modelSpace)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null && ent.LayerId == layerId)
                    {
                        count++;
                    }
                }

                tr.Commit();
                ed.WriteMessage($"\nChon cung layer: Tim thay {count} doi tuong tren layer '{layerName}'.");
                ed.WriteMessage("\n(Goi SELECT va dung FILTER layer de chon nhanh trong AutoCAD.)");
            }
        }

        public static void ShowConfigHint()
        {
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nCau hinh tu: Chuc nang day du co trong plugin release (PanelConfigurationWindow).");
        }

        public static void ExportCsvHint()
        {
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nXuat CSV: Chuc nang day du co trong plugin release (PanelCsvExporter).");
        }
    }
}
