using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    /// <summary>Ống gió HVAC nâng cao: ống mềm, co 45°, giảm tiết diện tự động.</summary>
    public static class MepHvacAdvancedService
    {
        private const string LayerName = "MEP_HVAC";
        private const short LayerColor = 4;
        private const double AngleToleranceDeg = 12.0;

        /// <summary>Ống gió đa điểm — tự phụ kiện co 45° / 90°, reducer khi đổi size.</summary>
        public static void DrawSmartDuctRun()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            double width = ReadSize("\nHVAC: Chieu rong ong (mm): ", 400);
            double height = ReadSize("\nHVAC: Chieu cao ong (mm): ", 250);

            PromptPointResult first = ed.GetPoint("\nHVAC: Diem dau ong gio: ");
            if (first.Status != PromptStatus.OK)
            {
                return;
            }

            var points = new List<Point3d> { first.Value };
            var widths = new List<double> { width };

            while (true)
            {
                var opts = new PromptPointOptions("\nHVAC: Diem tiep (Enter = ket thuc): ")
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
                widths.Add(widths[widths.Count - 1]);

                var kw = new PromptKeywordOptions("\nDoi size tai diem nay? [Khong] Co")
                {
                    AllowNone = true
                };
                kw.Keywords.Add("Khong");
                kw.Keywords.Add("Co");
                kw.Keywords.Default = "Khong";
                PromptResult change = ed.GetKeywords(kw);
                if (change.Status == PromptStatus.OK && change.StringResult == "Co")
                {
                    double newW = ReadSize("Chieu rong moi (mm): ", width * 0.75);
                    widths[widths.Count - 1] = newW;
                    width = newW;
                }
            }

            if (points.Count < 2)
            {
                return;
            }

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, LayerColor);
                BlockTableRecord ms = GetModelSpace(tr, doc.Database);

                int fittings = 0;
                for (int i = 0; i < points.Count - 1; i++)
                {
                    double w1 = widths[i];
                    double w2 = widths[i + 1];
                    DrawDuctSegment(ms, tr, layerId, points[i], points[i + 1], w1, w2, height);

                    if (Math.Abs(w1 - w2) > 1)
                    {
                        PlaceReducerBlock(doc.Database, tr, ms, layerId, points[i + 1],
                            points[i], points[Math.Min(i + 2, points.Count - 1)],
                            w1, w2, height);
                        fittings++;
                    }
                }

                for (int i = 1; i < points.Count - 1; i++)
                {
                    if (TryPlaceDuctFitting(doc.Database, tr, ms, layerId,
                        points[i - 1], points[i], points[i + 1], widths[i], height))
                    {
                        fittings++;
                    }
                }

                tr.Commit();
                ed.WriteMessage($"\nHVAC: Da ve {points.Count - 1} doan, {fittings} phu kien (co 45/90, reducer).");
            }
        }

        /// <summary>Ống gió mềm (flex) — nối AHU tới miệng gió.</summary>
        public static void DrawFlexibleDuct()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            double diameter = ReadSize("\nHVAC ong mem: Duong kinh (mm): ", 200);

            PromptPointResult start = ed.GetPoint("\nHVAC ong mem: Diem dau (may/co): ");
            if (start.Status != PromptStatus.OK)
            {
                return;
            }

            var endOpts = new PromptPointOptions("\nHVAC ong mem: Diem cuoi (mieng gio): ")
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
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, LayerColor);
                BlockTableRecord ms = GetModelSpace(tr, doc.Database);

                DrawFlexCurve(ms, tr, layerId, start.Value, end.Value, diameter);
                PlaceFlexEndCaps(doc.Database, ms, tr, layerId, start.Value, end.Value, diameter);

                tr.Commit();
                ed.WriteMessage("\nHVAC: Da ve ong mem + dau noi 2 dau.");
            }
        }

        /// <summary>Reducer thủ công — từ size lớn sang nhỏ.</summary>
        public static void DrawDuctReducerManual()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            double wLarge = ReadSize("\nReducer: Chieu rong lon (mm): ", 500);
            double wSmall = ReadSize("Chieu rong nho (mm): ", 300);
            double h = ReadSize("Chieu cao (mm): ", 250);

            PromptPointResult pick = ed.GetPoint("\nReducer: Vi tri (giua 2 doan ong): ");
            if (pick.Status != PromptStatus.OK)
            {
                return;
            }

            var dirOpts = new PromptPointOptions("\nHuong dong ong: ")
            {
                UseBasePoint = true,
                BasePoint = pick.Value
            };
            PromptPointResult dir = ed.GetPoint(dirOpts);
            if (dir.Status != PromptStatus.OK)
            {
                return;
            }

            double angle = Math.Atan2(dir.Value.Y - pick.Value.Y, dir.Value.X - pick.Value.X);
            Vector3d flow = (dir.Value - pick.Value).GetNormal();
            Point3d from = pick.Value - flow * 400;
            Point3d to = pick.Value + flow * 400;

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = MepDrawingHelper.EnsureLayer(doc.Database, tr, LayerName, LayerColor);
                BlockTableRecord ms = GetModelSpace(tr, doc.Database);

                DrawDuctSegment(ms, tr, layerId, from, pick.Value, wLarge, wLarge, h);
                DrawDuctSegment(ms, tr, layerId, pick.Value, to, wSmall, wSmall, h);
                PlaceReducerBlock(doc.Database, tr, ms, layerId, pick.Value, from, to, wLarge, wSmall, h, angle);

                tr.Commit();
                ed.WriteMessage("\nHVAC: Da ve reducer lon -> nho.");
            }
        }

        private static void DrawDuctSegment(
            BlockTableRecord ms,
            Transaction tr,
            ObjectId layerId,
            Point3d a,
            Point3d b,
            double widthStart,
            double widthEnd,
            double height)
        {
            double avgW = (widthStart + widthEnd) / 2;
            var duct = new Polyline(2)
            {
                LayerId = layerId,
                ConstantWidth = avgW * 0.4
            };
            duct.AddVertexAt(0, To2d(a), 0, avgW * 0.4, avgW * 0.4);
            duct.AddVertexAt(1, To2d(b), 0, avgW * 0.4, avgW * 0.4);
            ms.AppendEntity(duct);
            tr.AddNewlyCreatedDBObject(duct, true);

            // Viền ống chữ nhật (2 đường song song)
            Vector3d dir = (b - a).GetNormal();
            Vector3d perp = new Vector3d(-dir.Y, dir.X, 0) * (avgW / 2);
            var edge1 = new Line(a + perp, b + perp) { LayerId = layerId };
            var edge2 = new Line(a - perp, b - perp) { LayerId = layerId };
            ms.AppendEntity(edge1);
            ms.AppendEntity(edge2);
            tr.AddNewlyCreatedDBObject(edge1, true);
            tr.AddNewlyCreatedDBObject(edge2, true);
        }

        private static void DrawFlexCurve(
            BlockTableRecord ms,
            Transaction tr,
            ObjectId layerId,
            Point3d start,
            Point3d end,
            double diameter)
        {
            var flex = new Polyline();
            int segments = 12;
            Vector3d delta = end - start;
            Vector3d perp = new Vector3d(-delta.Y, delta.X, 0).GetNormal() * (diameter * 0.35);

            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                Point3d basePt = start + delta * t;
                double wave = Math.Sin(t * Math.PI * 3) * (diameter * 0.25);
                Point3d pt = basePt + perp * wave;
                flex.AddVertexAt(i, To2d(pt), 0, diameter * 0.35, diameter * 0.35);
            }

            flex.LayerId = layerId;
            ms.AppendEntity(flex);
            tr.AddNewlyCreatedDBObject(flex, true);
        }

        private static void PlaceFlexEndCaps(
            Database db,
            BlockTableRecord ms,
            Transaction tr,
            ObjectId layerId,
            Point3d start,
            Point3d end,
            double diameter)
        {
            ObjectId blockId = MepDrawingHelper.EnsureBlockDefinition(
                db,
                tr,
                "MEP_HVAC_FLEX_CONNECT",
                btr =>
                {
                    var c = new Circle(Point3d.Origin, Vector3d.ZAxis, diameter * 0.35) { LayerId = layerId };
                    btr.AppendEntity(c);
                    tr.AddNewlyCreatedDBObject(c, true);
                });

            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            foreach (Point3d pt in new[] { start, end })
            {
                var br = new BlockReference(pt, blockId) { LayerId = layerId, Rotation = angle };
                ms.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
            }
        }

        private static bool TryPlaceDuctFitting(
            Database db,
            Transaction tr,
            BlockTableRecord ms,
            ObjectId layerId,
            Point3d prev,
            Point3d corner,
            Point3d next,
            double width,
            double height)
        {
            Vector3d v1 = (prev - corner).GetNormal();
            Vector3d v2 = (next - corner).GetNormal();
            double turnRad = v1.GetAngleTo(v2);
            double turnDeg = turnRad * 180.0 / Math.PI;

            string blockName;
            if (turnDeg >= 90 - AngleToleranceDeg && turnDeg <= 90 + AngleToleranceDeg)
            {
                blockName = "MEP_HVAC_EL90";
            }
            else if (turnDeg >= 45 - AngleToleranceDeg && turnDeg <= 45 + AngleToleranceDeg)
            {
                blockName = "MEP_HVAC_EL45";
            }
            else
            {
                return false;
            }

            double rot = Math.Atan2(v1.Y, v1.X);
            ObjectId blockId = MepDrawingHelper.EnsureBlockDefinition(db, tr, blockName, btr =>
            {
                if (blockName.Contains("45"))
                {
                    BuildElbow45(btr, tr, layerId, width);
                }
                else
                {
                    BuildElbow90(btr, tr, layerId, width);
                }
            });

            var br = new BlockReference(corner, blockId) { LayerId = layerId, Rotation = rot };
            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            return true;
        }

        private static void PlaceReducerBlock(
            Database db,
            Transaction tr,
            BlockTableRecord ms,
            ObjectId layerId,
            Point3d at,
            Point3d from,
            Point3d to,
            double wLarge,
            double wSmall,
            double height,
            double? fixedAngle = null)
        {
            double angle = fixedAngle ?? Math.Atan2(to.Y - from.Y, to.X - from.X);
            ObjectId blockId = MepDrawingHelper.EnsureBlockDefinition(db, tr, "MEP_HVAC_REDUCER", btr =>
                BuildReducer(btr, tr, layerId, wLarge, wSmall, height));

            var br = new BlockReference(at, blockId) { LayerId = layerId, Rotation = angle };
            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
        }

        private static void BuildElbow90(BlockTableRecord btr, Transaction tr, ObjectId layerId, double w)
        {
            double r = w * 0.45;
            AddLine(btr, tr, layerId, Point3d.Origin, new Point3d(r, 0, 0));
            AddLine(btr, tr, layerId, Point3d.Origin, new Point3d(0, r, 0));
            var arc = new Arc(Point3d.Origin, r * 0.85, 0, Math.PI / 2) { LayerId = layerId };
            btr.AppendEntity(arc);
            tr.AddNewlyCreatedDBObject(arc, true);
        }

        private static void BuildElbow45(BlockTableRecord btr, Transaction tr, ObjectId layerId, double w)
        {
            double r = w * 0.5;
            AddLine(btr, tr, layerId, Point3d.Origin, new Point3d(r, 0, 0));
            double x45 = r * Math.Cos(Math.PI / 4);
            double y45 = r * Math.Sin(Math.PI / 4);
            AddLine(btr, tr, layerId, Point3d.Origin, new Point3d(x45, y45, 0));
            var arc = new Arc(Point3d.Origin, r * 0.85, 0, Math.PI / 4) { LayerId = layerId };
            btr.AppendEntity(arc);
            tr.AddNewlyCreatedDBObject(arc, true);
        }

        private static void BuildReducer(BlockTableRecord btr, Transaction tr, ObjectId layerId, double wL, double wS, double h)
        {
            var trap = new Polyline(4) { LayerId = layerId, Closed = true };
            trap.AddVertexAt(0, new Point2d(-wL / 2, -h / 4), 0, 0, 0);
            trap.AddVertexAt(1, new Point2d(wL / 2, -h / 4), 0, 0, 0);
            trap.AddVertexAt(2, new Point2d(wS / 2, h / 4), 0, 0, 0);
            trap.AddVertexAt(3, new Point2d(-wS / 2, h / 4), 0, 0, 0);
            btr.AppendEntity(trap);
            tr.AddNewlyCreatedDBObject(trap, true);
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

        private static double ReadSize(string prompt, double defaultVal)
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

        private static Point2d To2d(Point3d p) => new Point2d(p.X, p.Y);
    }
}
