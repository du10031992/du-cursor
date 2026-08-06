using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    /// <summary>
    /// Engine tự động bố trí phụ kiện khi vẽ ống nước / PCCC.
    /// Đọc block AMC từ DWG template tại runtime → map theo góc / giao điểm / DN.
    /// </summary>
    public static class MepPipeAutoLayoutService
    {
        private const double AngleTol = 15.0;   // tolerance góc (°)
        private const double SnapTol  = 1.0;    // mm — snap giao điểm

        // ─── AMC Block Name Catalog ─────────────────────────────────────
        // Pattern khớp với tên block trong AMC_TEMPLATE_RV29.dwg.
        // Khi AutoCAD load: ImportAmcLibrary() copy block vào bản vẽ hiện tại.
        // ResolveFittingBlockId() ưu tiên block AMC; nếu thiếu → tự tạo geometry.
        private static readonly Dictionary<(MepPipeSystem sys, MepPipeFittingKind kind), string[]> AmcPatterns
            = new Dictionary<(MepPipeSystem, MepPipeFittingKind), string[]>
        {
            // ── Hệ nước ──────────────────────────────────────────────────
            [(MepPipeSystem.Water, MepPipeFittingKind.Elbow90)]  = new[] { "*CO*90*NUOC*", "*ELBOW*90*WATER*", "*ONG*CO*90*", "*W-EL90*" },
            [(MepPipeSystem.Water, MepPipeFittingKind.Elbow45)]  = new[] { "*CO*45*NUOC*", "*ELBOW*45*WATER*", "*ONG*CO*45*", "*W-EL45*" },
            [(MepPipeSystem.Water, MepPipeFittingKind.Tee)]      = new[] { "*TEE*NUOC*", "*CHU*T*NUOC*", "*ONG*T*NUOC*", "*W-TEE*", "*TEE*WATER*" },
            [(MepPipeSystem.Water, MepPipeFittingKind.Reducer)]  = new[] { "*REDUC*NUOC*", "*GIAM*NUOC*", "*CO*THU*", "*W-REDUC*" },
            [(MepPipeSystem.Water, MepPipeFittingKind.Valve)]    = new[] { "*VAN*NUOC*", "*VALVE*WATER*", "*GATE*NUOC*", "*W-VAN*" },
            [(MepPipeSystem.Water, MepPipeFittingKind.Cap)]      = new[] { "*NUT*BIT*", "*CAP*NUOC*", "*W-CAP*" },

            // ── PCCC ─────────────────────────────────────────────────────
            [(MepPipeSystem.Fire, MepPipeFittingKind.Elbow90)]   = new[] { "*CO*90*PCCC*", "*ELBOW*90*FIRE*", "*F-EL90*" },
            [(MepPipeSystem.Fire, MepPipeFittingKind.Elbow45)]   = new[] { "*CO*45*PCCC*", "*ELBOW*45*FIRE*", "*F-EL45*" },
            [(MepPipeSystem.Fire, MepPipeFittingKind.Tee)]       = new[] { "*TEE*PCCC*", "*CHU*T*PCCC*", "*F-TEE*", "*TEE*FIRE*" },
            [(MepPipeSystem.Fire, MepPipeFittingKind.Reducer)]   = new[] { "*REDUC*PCCC*", "*GIAM*PCCC*", "*F-REDUC*" },
            [(MepPipeSystem.Fire, MepPipeFittingKind.Valve)]     = new[] { "*VAN*PCCC*", "*VALVE*FIRE*", "*F-VAN*" },
            [(MepPipeSystem.Fire, MepPipeFittingKind.Sprinkler)] = new[] { "*SPRINK*", "*SPK*", "*PHUN*", "*DAU*PHUN*", "*F-SPK*" },
            [(MepPipeSystem.Fire, MepPipeFittingKind.Hydrant)]   = new[] { "*HYDR*", "*CHUA*CHAY*", "*HCT*", "*F-HYD*" },
            [(MepPipeSystem.Fire, MepPipeFittingKind.Detector)]  = new[] { "*DETEC*", "*BAO*CHAY*", "*SMOKE*", "*F-DET*" },
        };

        // ─── Main: Smart Draw with Auto Fittings ───────────────────────
        public static void DrawSmartPipeRun(MepPipeSystem system)
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;
            string sysLabel = system == MepPipeSystem.Water ? "He nuoc" : "PCCC";

            // Nhập DN ống
            double dn = ReadDn(ed, sysLabel);

            // Nhập nhiều điểm
            var points = CollectPoints(ed, sysLabel);
            if (points.Count < 2) return;

            // Nhập vị trí valve (tùy chọn)
            var valvePositions = AskValvePositions(ed, sysLabel, points);

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = MepDrawingHelper.EnsureLayer(
                    doc.Database, tr,
                    system == MepPipeSystem.Water ? "MEP_WATER" : "MEP_FIRE",
                    system == MepPipeSystem.Water ? (short)5 : (short)1);

                ObjectId layerTagId = MepDrawingHelper.EnsureLayer(
                    doc.Database, tr, "MEP_TAG", 7);

                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(
                    bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // 1. Vẽ đường ống trung tâm (centerline)
                DrawPipeCenterline(ms, tr, layerId, points, dn);

                // 2. Tự động phụ kiện tại mỗi góc
                int fittingCount = 0;
                for (int i = 1; i < points.Count - 1; i++)
                {
                    var kind = ClassifyCorner(points[i - 1], points[i], points[i + 1]);
                    if (kind.HasValue)
                    {
                        PlaceFittingAtPoint(doc.Database, tr, ms, system, kind.Value, layerId,
                            points[i - 1], points[i], points[i + 1], dn);
                        fittingCount++;
                    }
                }

                // 3. Tự động Tee tại giao điểm với ống hiện có
                int teeCount = DetectAndPlaceTees(doc.Database, tr, ms, system, layerId, points, dn);
                fittingCount += teeCount;

                // 4. Van tại vị trí do người dùng chọn
                foreach (int idx in valvePositions)
                {
                    if (idx < points.Count)
                    {
                        PlaceValve(doc.Database, tr, ms, system, layerId, points, idx);
                    }
                }

                // 5. Nút bít tại đầu cuối (nếu không nối tiếp)
                PlaceCapAtEnd(doc.Database, tr, ms, system, layerId,
                    points[points.Count - 1], points[points.Count - 2], dn);
                fittingCount++;

                // 6. Gắn nhãn DN dọc theo ống
                TagPipeSegments(ms, tr, layerTagId, points, dn, sysLabel);

                tr.Commit();
                ed.WriteMessage(
                    $"\n[MEP] {sysLabel} DN{(int)dn}: {points.Count - 1} doan ong, " +
                    $"{fittingCount} phu kien (co 90/45, tee, van, nut) tu dong dat.");
            }
        }

        // ─── Corner classification ─────────────────────────────────────
        private static MepPipeFittingKind? ClassifyCorner(Point3d prev, Point3d corner, Point3d next)
        {
            Vector3d v1 = (prev - corner).GetNormal();
            Vector3d v2 = (next - corner).GetNormal();
            double angleDeg = v1.GetAngleTo(v2) * 180.0 / Math.PI;

            if (angleDeg >= 90 - AngleTol && angleDeg <= 90 + AngleTol)
                return MepPipeFittingKind.Elbow90;
            if (angleDeg >= 45 - AngleTol && angleDeg <= 45 + AngleTol)
                return MepPipeFittingKind.Elbow45;
            return null;
        }

        // ─── Place fitting at corner ───────────────────────────────────
        private static void PlaceFittingAtPoint(
            Database db, Transaction tr, BlockTableRecord ms,
            MepPipeSystem system, MepPipeFittingKind kind, ObjectId layerId,
            Point3d prev, Point3d corner, Point3d next, double dn)
        {
            Vector3d v1 = (prev - corner).GetNormal();
            double rot = Math.Atan2(v1.Y, v1.X) - Math.PI / 2;
            double scale = dn / 50.0;

            ObjectId blockId = ResolveAmcBlock(db, tr, system, kind, layerId);
            var br = new BlockReference(corner, blockId)
            {
                LayerId  = layerId,
                Rotation = rot,
                ScaleFactors = new Scale3d(scale, scale, scale)
            };
            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
        }

        // ─── Auto Tee detection ────────────────────────────────────────
        private static int DetectAndPlaceTees(
            Database db, Transaction tr, BlockTableRecord ms,
            MepPipeSystem system, ObjectId layerId,
            List<Point3d> newPipePoints, double dn)
        {
            string layerName = system == MepPipeSystem.Water ? "MEP_WATER" : "MEP_FIRE";
            int count = 0;

            // Quét tất cả Polyline hiện có trên layer MEP_WATER / MEP_FIRE
            foreach (ObjectId id in ms)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is Polyline pl))
                    continue;
                if (!string.Equals(pl.Layer, layerName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Kiểm tra từng điểm trên ống mới có giao với ống cũ không
                for (int newIdx = 1; newIdx < newPipePoints.Count - 1; newIdx++)
                {
                    Point3d pt = newPipePoints[newIdx];
                    if (IsPointOnPolyline(pl, pt, SnapTol * 5))
                    {
                        // Xác định hướng nhánh
                        Vector3d branchDir = (newPipePoints[newIdx + 1] - pt).GetNormal();
                        double rot = Math.Atan2(branchDir.Y, branchDir.X);
                        double scale = dn / 50.0;

                        ObjectId blockId = ResolveAmcBlock(db, tr, system, MepPipeFittingKind.Tee, layerId);
                        var br = new BlockReference(pt, blockId)
                        {
                            LayerId  = layerId,
                            Rotation = rot,
                            ScaleFactors = new Scale3d(scale, scale, scale)
                        };
                        ms.AppendEntity(br);
                        tr.AddNewlyCreatedDBObject(br, true);
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool IsPointOnPolyline(Polyline pl, Point3d pt, double tol)
        {
            Point3d closest = pl.GetClosestPointTo(pt, false);
            return closest.DistanceTo(pt) <= tol;
        }

        // ─── Place valve ───────────────────────────────────────────────
        private static void PlaceValve(
            Database db, Transaction tr, BlockTableRecord ms,
            MepPipeSystem system, ObjectId layerId,
            List<Point3d> points, int segIndex)
        {
            if (segIndex >= points.Count - 1) return;
            Point3d a = points[segIndex];
            Point3d b = points[segIndex + 1];
            Point3d mid = new Point3d((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
            double rot = Math.Atan2(b.Y - a.Y, b.X - a.X);

            ObjectId blockId = ResolveAmcBlock(db, tr, system, MepPipeFittingKind.Valve, layerId);
            var br = new BlockReference(mid, blockId)
            {
                LayerId  = layerId,
                Rotation = rot
            };
            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
        }

        // ─── Place cap at end ──────────────────────────────────────────
        private static void PlaceCapAtEnd(
            Database db, Transaction tr, BlockTableRecord ms,
            MepPipeSystem system, ObjectId layerId,
            Point3d end, Point3d prevPoint, double dn)
        {
            Vector3d dir = (end - prevPoint).GetNormal();
            double rot = Math.Atan2(dir.Y, dir.X);
            double scale = dn / 50.0;

            ObjectId blockId = ResolveAmcBlock(db, tr, system, MepPipeFittingKind.Cap, layerId);
            var br = new BlockReference(end, blockId)
            {
                LayerId  = layerId,
                Rotation = rot,
                ScaleFactors = new Scale3d(scale, scale, scale)
            };
            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
        }

        // ─── Pipe centerline ───────────────────────────────────────────
        private static void DrawPipeCenterline(
            BlockTableRecord ms, Transaction tr,
            ObjectId layerId, List<Point3d> points, double dn)
        {
            double pipeWidth = dn * 0.5;  // hiển thị theo tỷ lệ

            var pl = new Polyline(points.Count)
            {
                LayerId = layerId,
                ConstantWidth = pipeWidth,
                Linetype = "Continuous"
            };
            for (int i = 0; i < points.Count; i++)
            {
                pl.AddVertexAt(i, new Point2d(points[i].X, points[i].Y),
                    0, pipeWidth, pipeWidth);
            }
            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);

            // Đường tâm (centerline — nét đứt)
            var center = new Polyline(points.Count)
            {
                LayerId = layerId,
                ConstantWidth = 0
            };
            for (int i = 0; i < points.Count; i++)
            {
                center.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
            }
            center.Linetype = "CENTER";
            ms.AppendEntity(center);
            tr.AddNewlyCreatedDBObject(center, true);
        }

        // ─── DN label along pipe ───────────────────────────────────────
        private static void TagPipeSegments(
            BlockTableRecord ms, Transaction tr,
            ObjectId tagLayerId, List<Point3d> points, double dn, string sysLabel)
        {
            // Gắn nhãn mỗi đoạn ống
            for (int i = 0; i < points.Count - 1; i++)
            {
                Point3d a = points[i];
                Point3d b = points[i + 1];
                Point3d mid = new Point3d((a.X + b.X) / 2, (a.Y + b.Y) / 2, 0);
                double angle = Math.Atan2(b.Y - a.Y, b.X - a.X);
                double length = a.DistanceTo(b);

                // Offset vuông góc lên trên 120mm
                double offsetDist = 120;
                double ox = -Math.Sin(angle) * offsetDist;
                double oy = Math.Cos(angle) * offsetDist;

                var tag = new DBText
                {
                    LayerId  = tagLayerId,
                    TextString = $"DN{(int)dn}  L={length / 1000:0.00}m",
                    Height   = 80,
                    Position = new Point3d(mid.X + ox, mid.Y + oy, 0),
                    Rotation = angle > Math.PI / 2 || angle < -Math.PI / 2 ? angle + Math.PI : angle
                };
                ms.AppendEntity(tag);
                tr.AddNewlyCreatedDBObject(tag, true);
            }
        }

        // ─── Resolve AMC block → fallback auto-geometry ───────────────
        private static ObjectId ResolveAmcBlock(
            Database db, Transaction tr,
            MepPipeSystem system, MepPipeFittingKind kind,
            ObjectId layerId)
        {
            // 1. Tìm block AMC đã import
            string[] pats;
            if (AmcPatterns.TryGetValue((system, kind), out pats))
            {
                string found = MepDrawingHelper.FindBlockNameByPatterns(db, tr, pats);
                if (!string.IsNullOrEmpty(found))
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    return bt[found];
                }
            }

            // 2. Không có → dùng resolver cũ (tự tạo geometry)
            return MepPipeLibraryService.ResolveFittingBlockId(db, tr, system, kind, layerId);
        }

        // ─── UI Helpers ────────────────────────────────────────────────
        private static double ReadDn(Editor ed, string label)
        {
            var opts = new PromptKeywordOptions(
                $"\n{label}: Duong kinh ong [DN20] DN25 DN32 DN40 DN50 DN65 DN80 DN100 DN150")
            {
                AllowNone = true
            };
            foreach (string kw in new[] { "DN20", "DN25", "DN32", "DN40", "DN50", "DN65", "DN80", "DN100", "DN150" })
            {
                opts.Keywords.Add(kw);
            }
            opts.Keywords.Default = "DN50";

            PromptResult res = ed.GetKeywords(opts);
            if (res.Status == PromptStatus.OK)
            {
                string kw = res.StringResult?.Replace("DN", "") ?? "50";
                if (double.TryParse(kw, out double val)) return val;
            }
            return 50;
        }

        private static List<Point3d> CollectPoints(Editor ed, string label)
        {
            var pts = new List<Point3d>();

            PromptPointResult first = ed.GetPoint($"\n{label}: Diem dau ong: ");
            if (first.Status != PromptStatus.OK) return pts;
            pts.Add(first.Value);

            while (true)
            {
                var po = new PromptPointOptions(
                    $"\n{label}: Diem tiep theo (Enter = ket thuc): ")
                {
                    UseBasePoint = true,
                    BasePoint    = pts[pts.Count - 1],
                    AllowNone    = true
                };
                PromptPointResult next = ed.GetPoint(po);
                if (next.Status == PromptStatus.None) break;
                if (next.Status != PromptStatus.OK) return new List<Point3d>();
                pts.Add(next.Value);
            }

            return pts;
        }

        private static List<int> AskValvePositions(Editor ed, string label, List<Point3d> points)
        {
            var result = new List<int>();
            if (points.Count < 2) return result;

            var ko = new PromptKeywordOptions(
                $"\n{label}: Dat van tren doan ong nao? (Enter = khong) [Khong] Doan1 Doan2 Doan3 TatCa")
            {
                AllowNone = true
            };
            ko.Keywords.Add("Khong");
            ko.Keywords.Add("Doan1");
            ko.Keywords.Add("Doan2");
            ko.Keywords.Add("Doan3");
            ko.Keywords.Add("TatCa");
            ko.Keywords.Default = "Khong";

            PromptResult res = ed.GetKeywords(ko);
            if (res.Status != PromptStatus.OK || res.StringResult == "Khong")
                return result;

            if (res.StringResult == "TatCa")
            {
                for (int i = 0; i < points.Count - 1; i++) result.Add(i);
            }
            else if (res.StringResult == "Doan1") result.Add(0);
            else if (res.StringResult == "Doan2") result.Add(1);
            else if (res.StringResult == "Doan3") result.Add(2);

            return result;
        }
    }
}
