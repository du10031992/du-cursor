using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    /// <summary>
    /// Thư viện ống AMC — import block từ template, tự tạo phụ kiện, vẽ ống + đặt fitting.
    /// </summary>
    public static class MepPipeLibraryService
    {
        private static readonly Dictionary<MepPipeSystem, string> LayerBySystem = new Dictionary<MepPipeSystem, string>
        {
            [MepPipeSystem.Water] = "MEP_WATER",
            [MepPipeSystem.Fire] = "MEP_FIRE"
        };

        private static readonly Dictionary<MepPipeSystem, short> ColorBySystem = new Dictionary<MepPipeSystem, short>
        {
            [MepPipeSystem.Water] = 5,
            [MepPipeSystem.Fire] = 1
        };

        private static readonly Dictionary<MepPipeSystem, double> WidthBySystem = new Dictionary<MepPipeSystem, double>
        {
            [MepPipeSystem.Water] = 50,
            [MepPipeSystem.Fire] = 65
        };

        public static void ImportAmcLibrary()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;
            string path = MepPluginConfig.PipeLibraryDwg;

            if (!File.Exists(path))
            {
                ed.WriteMessage("\n[MEP] Khong tim thay thu vien: " + path);
                ed.WriteMessage("\nDat pipeLibraryDwg trong MepPanel.config.json hoac copy AMC_TEMPLATE_RV29.dwg vao samples/templates/");
                return;
            }

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                int imported = MepDrawingHelper.ImportPipeBlocksFromDwg(
                    doc.Database,
                    tr,
                    path,
                    GetAllSearchPatterns());
                tr.Commit();
                ed.WriteMessage($"\n[MEP] Da nap {imported} block phu kien tu thu vien AMC.");
                ed.WriteMessage("\nFile: " + path);
            }
        }

        public static void DrawPipeRunWithFittings(MepPipeSystem system)
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;
            string label = system == MepPipeSystem.Water ? "He nuoc" : "PCCC";

            PromptPointResult first = ed.GetPoint($"\n{label}: Diem dau ong: ");
            if (first.Status != PromptStatus.OK)
            {
                return;
            }

            var points = new List<Point3d> { first.Value };
            while (true)
            {
                var opts = new PromptPointOptions($"\n{label}: Diem tiep theo (Enter = ket thuc): ")
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
                ed.WriteMessage("\nCan it nhat 2 diem de ve ong.");
                return;
            }

            double width = WidthBySystem[system];
            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                ObjectId layerId = MepDrawingHelper.EnsureLayer(
                    doc.Database, tr, LayerBySystem[system], ColorBySystem[system]);

                BlockTable blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    blockTable[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite);

                var pipe = new Polyline(points.Count) { LayerId = layerId, ConstantWidth = width };
                for (int i = 0; i < points.Count; i++)
                {
                    pipe.AddVertexAt(i, To2d(points[i]), 0, width, width);
                }

                modelSpace.AppendEntity(pipe);
                tr.AddNewlyCreatedDBObject(pipe, true);

                int fittings = 0;
                for (int i = 1; i < points.Count - 1; i++)
                {
                    if (TryPlaceAutoElbow(
                        doc.Database, tr, modelSpace, system, layerId,
                        points[i - 1], points[i], points[i + 1]))
                    {
                        fittings++;
                    }
                }

                tr.Commit();
                ed.WriteMessage($"\n{label}: Da ve {points.Count - 1} doan ong, tu dat {fittings} co 90.");
            }
        }

        public static void PlaceFitting(MepPipeSystem system, MepPipeFittingKind? fixedKind = null)
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;
            string label = system == MepPipeSystem.Water ? "He nuoc" : "PCCC";

            MepPipeFittingKind kind;
            if (fixedKind.HasValue)
            {
                kind = fixedKind.Value;
            }
            else
            {
                var kinds = system == MepPipeSystem.Water
                    ? new[] { MepPipeFittingKind.Elbow90, MepPipeFittingKind.Tee, MepPipeFittingKind.Reducer, MepPipeFittingKind.Valve, MepPipeFittingKind.Cap }
                    : new[] { MepPipeFittingKind.Elbow90, MepPipeFittingKind.Tee, MepPipeFittingKind.Valve, MepPipeFittingKind.Sprinkler, MepPipeFittingKind.Hydrant, MepPipeFittingKind.Detector };

                var pko = new PromptKeywordOptions($"\n{label} — chon phu kien")
                {
                    AllowNone = false
                };
                foreach (MepPipeFittingKind k in kinds)
                {
                    pko.Keywords.Add(GetKeyword(k));
                }

                pko.Keywords.Default = GetKeyword(MepPipeFittingKind.Elbow90);
                PromptResult kindResult = ed.GetKeywords(pko);
                if (kindResult.Status != PromptStatus.OK)
                {
                    return;
                }

                kind = ParseKeyword(kindResult.StringResult);
            }

            PromptPointResult ins = ed.GetPoint($"\n{label}: Vi tri dat {GetDisplayName(kind)}: ");
            if (ins.Status != PromptStatus.OK)
            {
                return;
            }

            var rotOpts = new PromptPointOptions("\nHuong ong (diem thu 2): ")
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
                ObjectId layerId = MepDrawingHelper.EnsureLayer(
                    doc.Database, tr, LayerBySystem[system], ColorBySystem[system]);

                BlockTable blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                    blockTable[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite);

                ObjectId blockId = ResolveFittingBlockId(doc.Database, tr, system, kind, layerId);
                var blockRef = new BlockReference(ins.Value, blockId)
                {
                    LayerId = layerId,
                    Rotation = angle
                };
                modelSpace.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);
                tr.Commit();

                ed.WriteMessage($"\n{label}: Da dat {GetDisplayName(kind)}.");
            }
        }

        internal static ObjectId ResolveFittingBlockId(
            Database db,
            Transaction tr,
            MepPipeSystem system,
            MepPipeFittingKind kind,
            ObjectId layerId)
        {
            string[] patterns = GetPatterns(system, kind);
            string existing = MepDrawingHelper.FindBlockNameByPatterns(db, tr, patterns);
            if (!string.IsNullOrEmpty(existing))
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                return bt[existing];
            }

            string autoName = GetAutoBlockName(system, kind);
            double size = WidthBySystem[system];
            return MepDrawingHelper.EnsureBlockDefinition(db, tr, autoName, btr =>
                BuildFittingGeometry(btr, tr, kind, layerId, size));
        }

        private static bool TryPlaceAutoElbow(
            Database db,
            Transaction tr,
            BlockTableRecord modelSpace,
            MepPipeSystem system,
            ObjectId layerId,
            Point3d prev,
            Point3d corner,
            Point3d next)
        {
            Vector3d v1 = prev - corner;
            Vector3d v2 = next - corner;
            if (v1.Length < 1e-3 || v2.Length < 1e-3)
            {
                return false;
            }

            double angleDeg = Math.Abs(v1.GetAngleTo(v2) * 180.0 / Math.PI);
            if (angleDeg < 45 || angleDeg > 135)
            {
                return false;
            }

            double rot = Math.Atan2(v1.Y, v1.X) + Math.PI / 2;
            ObjectId blockId = ResolveFittingBlockId(db, tr, system, MepPipeFittingKind.Elbow90, layerId);
            var blockRef = new BlockReference(corner, blockId) { LayerId = layerId, Rotation = rot };
            modelSpace.AppendEntity(blockRef);
            tr.AddNewlyCreatedDBObject(blockRef, true);
            return true;
        }

        private static void BuildFittingGeometry(
            BlockTableRecord btr,
            Transaction tr,
            MepPipeFittingKind kind,
            ObjectId layerId,
            double size)
        {
            double r = size * 2;
            switch (kind)
            {
                case MepPipeFittingKind.Elbow90:
                    AddLine(btr, tr, layerId, new Point3d(0, 0, 0), new Point3d(r, 0, 0));
                    AddLine(btr, tr, layerId, new Point3d(0, 0, 0), new Point3d(0, r, 0));
                    AddArc(btr, tr, layerId, new Point3d(0, 0, 0), size, 0, Math.PI / 2);
                    break;

                case MepPipeFittingKind.Tee:
                    AddLine(btr, tr, layerId, new Point3d(-r, 0, 0), new Point3d(r, 0, 0));
                    AddLine(btr, tr, layerId, new Point3d(0, 0, 0), new Point3d(0, r, 0));
                    break;

                case MepPipeFittingKind.Reducer:
                    var reducer = new Polyline(4) { LayerId = layerId, Closed = true };
                    reducer.AddVertexAt(0, new Point2d(-r, -size / 2), 0, 0, 0);
                    reducer.AddVertexAt(1, new Point2d(r, -size / 4), 0, 0, 0);
                    reducer.AddVertexAt(2, new Point2d(r, size / 4), 0, 0, 0);
                    reducer.AddVertexAt(3, new Point2d(-r, size / 2), 0, 0, 0);
                    btr.AppendEntity(reducer);
                    tr.AddNewlyCreatedDBObject(reducer, true);
                    break;

                case MepPipeFittingKind.Valve:
                    AddLine(btr, tr, layerId, new Point3d(-r, 0, 0), new Point3d(-size / 2, 0, 0));
                    AddLine(btr, tr, layerId, new Point3d(size / 2, 0, 0), new Point3d(r, 0, 0));
                    var bow1 = new Line(new Point3d(-size / 2, 0, 0), new Point3d(0, size / 2, 0)) { LayerId = layerId };
                    var bow2 = new Line(new Point3d(0, size / 2, 0), new Point3d(size / 2, 0, 0)) { LayerId = layerId };
                    var bow3 = new Line(new Point3d(-size / 2, 0, 0), new Point3d(0, -size / 2, 0)) { LayerId = layerId };
                    var bow4 = new Line(new Point3d(0, -size / 2, 0), new Point3d(size / 2, 0, 0)) { LayerId = layerId };
                    foreach (Line ln in new[] { bow1, bow2, bow3, bow4 })
                    {
                        btr.AppendEntity(ln);
                        tr.AddNewlyCreatedDBObject(ln, true);
                    }
                    break;

                case MepPipeFittingKind.Cap:
                    AddCircle(btr, tr, layerId, Point3d.Origin, size * 0.8);
                    AddLine(btr, tr, layerId, new Point3d(-r, 0, 0), new Point3d(0, 0, 0));
                    break;

                case MepPipeFittingKind.Sprinkler:
                    AddCircle(btr, tr, layerId, Point3d.Origin, size * 0.6);
                    AddLine(btr, tr, layerId, new Point3d(0, 0, 0), new Point3d(0, -r, 0));
                    AddLine(btr, tr, layerId, new Point3d(-size / 3, -r * 0.6, 0), new Point3d(size / 3, -r * 0.6, 0));
                    break;

                case MepPipeFittingKind.Hydrant:
                    AddRect(btr, tr, layerId, -size / 2, -size, size / 2, size);
                    AddLine(btr, tr, layerId, new Point3d(0, size, 0), new Point3d(0, r, 0));
                    break;

                case MepPipeFittingKind.Detector:
                    AddCircle(btr, tr, layerId, Point3d.Origin, size);
                    AddCircle(btr, tr, layerId, Point3d.Origin, size * 0.35);
                    break;
            }
        }

        private static void AddLine(BlockTableRecord btr, Transaction tr, ObjectId layerId, Point3d a, Point3d b)
        {
            var line = new Line(a, b) { LayerId = layerId };
            btr.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
        }

        private static void AddArc(BlockTableRecord btr, Transaction tr, ObjectId layerId, Point3d center, double radius, double start, double end)
        {
            var arc = new Arc(center, radius, start, end) { LayerId = layerId };
            btr.AppendEntity(arc);
            tr.AddNewlyCreatedDBObject(arc, true);
        }

        private static void AddCircle(BlockTableRecord btr, Transaction tr, ObjectId layerId, Point3d center, double radius)
        {
            var circle = new Circle(center, Vector3d.ZAxis, radius) { LayerId = layerId };
            btr.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);
        }

        private static void AddRect(BlockTableRecord btr, Transaction tr, ObjectId layerId, double x1, double y1, double x2, double y2)
        {
            var rect = new Polyline(4) { LayerId = layerId, Closed = true };
            rect.AddVertexAt(0, new Point2d(x1, y1), 0, 0, 0);
            rect.AddVertexAt(1, new Point2d(x2, y1), 0, 0, 0);
            rect.AddVertexAt(2, new Point2d(x2, y2), 0, 0, 0);
            rect.AddVertexAt(3, new Point2d(x1, y2), 0, 0, 0);
            btr.AppendEntity(rect);
            tr.AddNewlyCreatedDBObject(rect, true);
        }

        private static string GetAutoBlockName(MepPipeSystem system, MepPipeFittingKind kind)
        {
            string prefix = system == MepPipeSystem.Water ? "MEP_WATER" : "MEP_FIRE";
            return prefix + "_FIT_" + kind.ToString().ToUpperInvariant();
        }

        private static string[] GetPatterns(MepPipeSystem system, MepPipeFittingKind kind)
        {
            if (system == MepPipeSystem.Water)
            {
                switch (kind)
                {
                    case MepPipeFittingKind.Elbow90: return new[] { "*CO*90*", "*ELBOW*", "*ONG*CO*", "*WATER*ELBOW*" };
                    case MepPipeFittingKind.Tee: return new[] { "*TEE*", "*CHU*T*", "*ONG*T*", "*WATER*TEE*" };
                    case MepPipeFittingKind.Reducer: return new[] { "*REDUC*", "*GIAM*", "*CO*THU*" };
                    case MepPipeFittingKind.Valve: return new[] { "*VALVE*", "*VAN*", "*WATER*VAN*" };
                    case MepPipeFittingKind.Cap: return new[] { "*CAP*", "*NUOT*", "*BIT*" };
                    default: return Array.Empty<string>();
                }
            }

            switch (kind)
            {
                case MepPipeFittingKind.Elbow90: return new[] { "*CO*90*", "*ELBOW*", "*PCCC*CO*", "*FIRE*ELBOW*" };
                case MepPipeFittingKind.Tee: return new[] { "*TEE*", "*PCCC*T*", "*FIRE*TEE*" };
                case MepPipeFittingKind.Valve: return new[] { "*VALVE*", "*VAN*", "*FIRE*VAN*", "*PCCC*VAN*" };
                case MepPipeFittingKind.Sprinkler: return new[] { "*SPRINK*", "*SPK*", "*PHUN*", "*DAM*PHUN*" };
                case MepPipeFittingKind.Hydrant: return new[] { "*HYDR*", "*CHUA*CHAY*", "*HCT*", "*FIRE*HYDR*" };
                case MepPipeFittingKind.Detector: return new[] { "*DETEC*", "*BAO*CHAY*", "*SMOKE*", "*FIRE*DET*" };
                default: return Array.Empty<string>();
            }
        }

        private static string[] GetAllSearchPatterns()
        {
            var list = new List<string>();
            foreach (MepPipeSystem sys in Enum.GetValues(typeof(MepPipeSystem)))
            {
                foreach (MepPipeFittingKind kind in Enum.GetValues(typeof(MepPipeFittingKind)))
                {
                    list.AddRange(GetPatterns(sys, kind));
                }
            }

            list.Add("*ONG*");
            list.Add("*PIPE*");
            list.Add("*NUOC*");
            list.Add("*WATER*");
            list.Add("*PCCC*");
            list.Add("*FIRE*");
            list.Add("*AMC*");
            return list.ToArray();
        }

        private static string GetKeyword(MepPipeFittingKind kind)
        {
            switch (kind)
            {
                case MepPipeFittingKind.Elbow90: return "Co90";
                case MepPipeFittingKind.Tee: return "Tee";
                case MepPipeFittingKind.Reducer: return "Giam";
                case MepPipeFittingKind.Valve: return "Van";
                case MepPipeFittingKind.Cap: return "Nut";
                case MepPipeFittingKind.Sprinkler: return "Sprinkler";
                case MepPipeFittingKind.Hydrant: return "ChuaChay";
                case MepPipeFittingKind.Detector: return "DauBao";
                default: return kind.ToString();
            }
        }

        private static MepPipeFittingKind ParseKeyword(string keyword)
        {
            switch (keyword?.Trim())
            {
                case "Co90": return MepPipeFittingKind.Elbow90;
                case "Tee": return MepPipeFittingKind.Tee;
                case "Giam": return MepPipeFittingKind.Reducer;
                case "Van": return MepPipeFittingKind.Valve;
                case "Nut": return MepPipeFittingKind.Cap;
                case "Sprinkler": return MepPipeFittingKind.Sprinkler;
                case "ChuaChay": return MepPipeFittingKind.Hydrant;
                case "DauBao": return MepPipeFittingKind.Detector;
                default: return MepPipeFittingKind.Elbow90;
            }
        }

        private static string GetDisplayName(MepPipeFittingKind kind)
        {
            switch (kind)
            {
                case MepPipeFittingKind.Elbow90: return "Co 90";
                case MepPipeFittingKind.Tee: return "Chu T";
                case MepPipeFittingKind.Reducer: return "Co giam";
                case MepPipeFittingKind.Valve: return "Van";
                case MepPipeFittingKind.Cap: return "Nut bit";
                case MepPipeFittingKind.Sprinkler: return "Dau phun";
                case MepPipeFittingKind.Hydrant: return "Chua chay";
                case MepPipeFittingKind.Detector: return "Dau bao";
                default: return kind.ToString();
            }
        }

        private static Point2d To2d(Point3d p) => new Point2d(p.X, p.Y);
    }
}
