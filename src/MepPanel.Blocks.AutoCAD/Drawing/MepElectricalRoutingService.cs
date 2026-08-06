using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    /// <summary>Máng cáp, trunking (ống luồn dây) — hệ điện thi công.</summary>
    public static class MepElectricalRoutingService
    {
        private const string TrayLayer = "MEP_CABLE_TRAY";
        private const string TrunkLayer = "MEP_TRUNKING";
        private const short TrayColor = 3;
        private const short TrunkColor = 6;

        public static void DrawCableTrayRun()
        {
            DrawRoutingRun(
                TrayLayer,
                TrayColor,
                "Mang cap",
                ReadWidth("Chieu rong mang cap (mm): ", 300),
                ReadHeight("Chieu cao mang cap (mm): ", 100),
                drawCover: true);
        }

        public static void DrawTrunkingRun()
        {
            DrawRoutingRun(
                TrunkLayer,
                TrunkColor,
                "Trunking",
                ReadWidth("Chieu rong trunking (mm): ", 100),
                ReadHeight("Chieu cao trunking (mm): ", 50),
                drawCover: false);
        }

        public static void PlaceTrayElbow90()
        {
            PlaceRoutingFitting(TrayLayer, TrayColor, "MEP_TRAY_EL90", 300, 100, true, BuildTrayElbow90);
        }

        public static void PlaceTrunkingElbow90()
        {
            PlaceRoutingFitting(TrunkLayer, TrunkColor, "MEP_TRUNK_EL90", 100, 50, false, BuildTrunkElbow90);
        }

        private static void DrawRoutingRun(
            string layerName,
            short color,
            string label,
            double widthMm,
            double heightMm,
            bool drawCover)
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            PromptPointResult first = ed.GetPoint($"\n{label}: Diem dau: ");
            if (first.Status != PromptStatus.OK)
            {
                return;
            }

            var points = new List<Point3d> { first.Value };
            while (true)
            {
                var opts = new PromptPointOptions($"\n{label}: Diem tiep (Enter = ket thuc): ")
                {
                    UseBasePoint = true,
                    BasePoint = points[points.Count - 1],
                    AllowNone = true
                };
                PromptPointResult next = ed.GetPoint(opts);
                if (next.Status == PromptStatus.None)
                {
                    break;
                }

                if (next.Status != PromptStatus.OK)
                {
                    return;
                }

                points.Add(next.Value);
            }

            if (points.Count < 2)
            {
                return;
            }

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, layerName, color);
                BlockTableRecord ms = GetModelSpace(tr, doc.Database);

                for (int i = 0; i < points.Count - 1; i++)
                {
                    DrawTraySegment(ms, tr, layerId, points[i], points[i + 1], widthMm, heightMm, drawCover);
                }

                for (int i = 1; i < points.Count - 1; i++)
                {
                    TryPlaceRoutingElbow(ms, tr, layerId, points[i - 1], points[i], points[i + 1], widthMm, heightMm, drawCover);
                }

                tr.Commit();
                ed.WriteMessage($"\n{label}: Da ve {points.Count - 1} doan, co tu dong tai goc 90.");
            }
        }

        private static void DrawTraySegment(
            BlockTableRecord ms,
            Transaction tr,
            ObjectId layerId,
            Point3d a,
            Point3d b,
            double width,
            double height,
            bool drawCover)
        {
            Vector3d dir = b - a;
            if (dir.Length < 1e-6)
            {
                return;
            }

            dir = dir.GetNormal();
            Vector3d perp = new Vector3d(-dir.Y, dir.X, 0) * (width / 2);

            var bottom = new Line(a + perp, b + perp) { LayerId = layerId };
            var top = new Line(a - perp, b - perp) { LayerId = layerId };
            ms.AppendEntity(bottom);
            ms.AppendEntity(top);
            tr.AddNewlyCreatedDBObject(bottom, true);
            tr.AddNewlyCreatedDBObject(top, true);

            // Vách đáy máng (hình chữ nhật mặt cắt đơn giản — 2 đường biên)
            if (drawCover)
            {
                var coverOffset = perp * 0.15;
                var cover = new Line(a - coverOffset, b - coverOffset)
                {
                    LayerId = layerId,
                    Linetype = "DASHED"
                };
                ms.AppendEntity(cover);
                tr.AddNewlyCreatedDBObject(cover, true);
            }

            // Nắp đầu đoạn (vạch vuông góc)
            var cap1 = new Line(a + perp, a - perp) { LayerId = layerId };
            ms.AppendEntity(cap1);
            tr.AddNewlyCreatedDBObject(cap1, true);
        }

        private static void TryPlaceRoutingElbow(
            BlockTableRecord ms,
            Transaction tr,
            ObjectId layerId,
            Point3d prev,
            Point3d corner,
            Point3d next,
            double width,
            double height,
            bool drawCover)
        {
            Vector3d v1 = (prev - corner).GetNormal();
            Vector3d v2 = (next - corner).GetNormal();
            double turnDeg = v1.GetAngleTo(v2) * 180.0 / Math.PI;
            if (turnDeg < 75 || turnDeg > 105)
            {
                return;
            }

            double r = width * 0.6;
            double a1 = Math.Atan2(v1.Y, v1.X);
            double a2 = Math.Atan2(v2.Y, v2.X);
            var arc = new Arc(corner, r, a1, a2) { LayerId = layerId };
            ms.AppendEntity(arc);
            tr.AddNewlyCreatedDBObject(arc, true);
        }

        private static void PlaceRoutingFitting(
            string layerName,
            short color,
            string blockName,
            double width,
            double height,
            bool isTray,
            Action<BlockTableRecord, Transaction, ObjectId, double, double, bool> builder)
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            PromptPointResult ins = ed.GetPoint("\nVi tri phu kien: ");
            if (ins.Status != PromptStatus.OK)
            {
                return;
            }

            var rotOpts = new PromptPointOptions("\nHuong (diem 2): ")
            {
                UseBasePoint = true,
                BasePoint = ins.Value
            };
            PromptPointResult dir = ed.GetPoint(rotOpts);
            if (dir.Status != PromptStatus.OK)
            {
                return;
            }

            double angle = Math.Atan2(dir.Value.Y - ins.Value.Y, dir.Value.X - ins.Value.X);

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, layerName, color);
                ObjectId blockId = MepDrawingHelper.EnsureBlockDefinition(doc.Database, tr, blockName, btr =>
                    builder(btr, tr, layerId, width, height, isTray));

                BlockTableRecord ms = GetModelSpace(tr, doc.Database);
                var br = new BlockReference(ins.Value, blockId) { LayerId = layerId, Rotation = angle };
                ms.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                tr.Commit();
            }

            ed.WriteMessage("\nDa dat phu kien mang/trunking.");
        }

        private static void BuildTrayElbow90(BlockTableRecord btr, Transaction tr, ObjectId layerId, double w, double h, bool isTray)
        {
            double r = w * 0.5;
            AddLine(btr, tr, layerId, Point3d.Origin, new Point3d(r, 0, 0));
            AddLine(btr, tr, layerId, Point3d.Origin, new Point3d(0, r, 0));
            var arc = new Arc(Point3d.Origin, r * 0.8, 0, Math.PI / 2) { LayerId = layerId };
            btr.AppendEntity(arc);
            tr.AddNewlyCreatedDBObject(arc, true);
        }

        private static void BuildTrunkElbow90(BlockTableRecord btr, Transaction tr, ObjectId layerId, double w, double h, bool isTray)
        {
            BuildTrayElbow90(btr, tr, layerId, w, h, isTray);
        }

        private static void AddLine(BlockTableRecord btr, Transaction tr, ObjectId layerId, Point3d a, Point3d b)
        {
            var ln = new Line(a, b) { LayerId = layerId };
            btr.AppendEntity(ln);
            tr.AddNewlyCreatedDBObject(ln, true);
        }

        private static BlockTableRecord GetModelSpace(Transaction tr, Database db)
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        }

        private static double ReadWidth(string prompt, double defaultVal)
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            var opts = new PromptDoubleOptions(prompt)
            {
                DefaultValue = defaultVal,
                UseDefaultValue = true,
                AllowNegative = false,
                AllowZero = false
            };
            PromptDoubleResult r = doc.Editor.GetDouble(opts);
            return r.Status == PromptStatus.OK ? r.Value : defaultVal;
        }

        private static double ReadHeight(string prompt, double defaultVal) => ReadWidth(prompt, defaultVal);
    }
}
