using System;
using System.Collections.Generic;
using System.IO;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

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

        public static string FindBlockNameByPatterns(Database db, Transaction tr, string[] patterns)
        {
            if (patterns == null || patterns.Length == 0)
            {
                return null;
            }

            BlockTable blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId id in blockTable)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (btr.IsLayout || btr.IsAnonymous)
                {
                    continue;
                }

                if (MatchesAnyPattern(btr.Name, patterns))
                {
                    return btr.Name;
                }
            }

            return null;
        }

        public static int ImportPipeBlocksFromDwg(
            Database targetDb,
            Transaction targetTr,
            string libraryDwgPath,
            string[] namePatterns)
        {
            if (!File.Exists(libraryDwgPath))
            {
                return 0;
            }

            var sourceIds = new ObjectIdCollection();
            using (var sourceDb = new Database(false, true))
            {
                sourceDb.ReadDwgFile(libraryDwgPath, FileShare.Read, true, null);

                using (Transaction sourceTr = sourceDb.TransactionManager.StartTransaction())
                {
                    BlockTable sourceBt = (BlockTable)sourceTr.GetObject(sourceDb.BlockTableId, OpenMode.ForRead);
                    BlockTable targetBt = (BlockTable)targetTr.GetObject(targetDb.BlockTableId, OpenMode.ForRead);

                    foreach (ObjectId id in sourceBt)
                    {
                        var btr = (BlockTableRecord)sourceTr.GetObject(id, OpenMode.ForRead);
                        if (btr.IsLayout || btr.IsAnonymous || btr.Name.StartsWith("*", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (!MatchesAnyPattern(btr.Name, namePatterns))
                        {
                            continue;
                        }

                        if (targetBt.Has(btr.Name))
                        {
                            continue;
                        }

                        sourceIds.Add(id);
                    }

                    sourceTr.Commit();
                }

                if (sourceIds.Count == 0)
                {
                    return 0;
                }

                var mapping = new IdMapping();
                targetDb.WblockCloneObjects(
                    sourceIds,
                    targetDb.BlockTableId,
                    mapping,
                    DuplicateRecordCloning.Replace,
                    false);
            }

            return sourceIds.Count;
        }

        private static bool MatchesAnyPattern(string blockName, string[] patterns)
        {
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return false;
            }

            foreach (string pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                if (LikeMatch(blockName, pattern))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LikeMatch(string text, string pattern)
        {
            string p = pattern.Trim().ToUpperInvariant();
            string t = text.Trim().ToUpperInvariant();

            if (p == "*")
            {
                return true;
            }

            if (!p.Contains("*"))
            {
                return t.Contains(p);
            }

            string[] parts = p.Split(new[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return true;
            }

            int index = 0;
            foreach (string part in parts)
            {
                int found = t.IndexOf(part, index, StringComparison.Ordinal);
                if (found < 0)
                {
                    return false;
                }

                index = found + part.Length;
            }

            return true;
        }
    }
}
