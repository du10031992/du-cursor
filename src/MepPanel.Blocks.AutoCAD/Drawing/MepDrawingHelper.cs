using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    internal static class MepDrawingHelper
    {
        public static Document GetActiveDocument()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                throw new InvalidOperationException("Khong co ban ve AutoCAD dang mo.");
            }

            return doc;
        }

        public static ObjectId EnsureLayer(Database db, Transaction tr, string layerName, short colorIndex)
        {
            LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (layerTable.Has(layerName))
            {
                return layerTable[layerName];
            }

            layerTable.UpgradeOpen();
            var layer = new LayerTableRecord
            {
                Name = layerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
            };
            ObjectId layerId = layerTable.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
            layerTable.DowngradeOpen();
            return layerId;
        }

        public static ObjectId EnsureBlockDefinition(
            Database db,
            Transaction tr,
            string blockName,
            Action<BlockTableRecord> buildContents)
        {
            BlockTable blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (blockTable.Has(blockName))
            {
                return blockTable[blockName];
            }

            blockTable.UpgradeOpen();
            var blockDef = new BlockTableRecord
            {
                Name = blockName,
                Origin = Autodesk.AutoCAD.Geometry.Point3d.Origin
            };
            ObjectId blockId = blockTable.Add(blockDef);
            tr.AddNewlyCreatedDBObject(blockDef, true);
            buildContents(blockDef);
            blockTable.DowngradeOpen();
            return blockId;
        }
    }
}
