using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using MepPanel.Core.Calculations;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    /// <summary>
    /// Render minh hoa he ong nuoc / PCCC bang Python/Pillow (so do + phan tich).
    /// </summary>
    public static class MepPipeRenderService
    {
        private const string RendererScript = "render_pipe_system.py";
        private const int TimeoutMs = 90000;

        public static void RenderWater() => RenderSystem(MepPipeSystem.Water);

        public static void RenderFire() => RenderSystem(MepPipeSystem.Fire);

        public static void RenderSystem(MepPipeSystem system)
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;
            string label = system == MepPipeSystem.Water ? "He nuoc" : "PCCC";

            var kw = new PromptKeywordOptions(
                "\nRender " + label + ": nguon du lieu [TuBanVe] Demo")
            {
                AllowNone = true
            };
            kw.Keywords.Add("TuBanVe");
            kw.Keywords.Add("Demo");
            kw.Keywords.Default = "TuBanVe";

            PromptResult mode = ed.GetKeywords(kw);
            string src = mode.Status == PromptStatus.OK ? mode.StringResult : "TuBanVe";

            string jsonPath = null;
            if (src != "Demo")
            {
                jsonPath = ExtractFromDrawing(doc, ed, system);
                if (jsonPath == null)
                {
                    ed.WriteMessage("\nKhong doc duoc ong tu ban ve. Dung Demo.");
                    return;
                }
            }

            string prefix = system == MepPipeSystem.Water ? "MEP_WATER" : "MEP_FIRE";
            string outPng = Path.Combine(
                Path.GetTempPath(),
                prefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");

            if (!CallPythonRenderer(ed, system, jsonPath, outPng))
            {
                return;
            }

            ed.WriteMessage("\n[MEP] Render " + label + " thanh cong: " + outPng);
            try
            {
                Process.Start(new ProcessStartInfo(outPng) { UseShellExecute = true });
            }
            catch
            {
                ed.WriteMessage("\nKhong mo duoc anh tu dong. File: " + outPng);
            }
        }

        private static string ExtractFromDrawing(
            Autodesk.AutoCAD.ApplicationServices.Document doc,
            Editor ed,
            MepPipeSystem system)
        {
            string layerName = system == MepPipeSystem.Water ? "MEP_WATER" : "MEP_FIRE";
            var segments = new List<PipeSegmentJson>();
            var fittings = new List<PipeFittingJson>();

            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(
                    bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in ms)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null)
                    {
                        continue;
                    }

                    string layer = ent.Layer ?? "";
                    bool onSystemLayer = layer.Equals(layerName, StringComparison.OrdinalIgnoreCase)
                        || (system == MepPipeSystem.Water && layer.IndexOf("WATER", StringComparison.OrdinalIgnoreCase) >= 0)
                        || (system == MepPipeSystem.Water && layer.IndexOf("NUOC", StringComparison.OrdinalIgnoreCase) >= 0)
                        || (system == MepPipeSystem.Fire && layer.IndexOf("FIRE", StringComparison.OrdinalIgnoreCase) >= 0)
                        || (system == MepPipeSystem.Fire && layer.IndexOf("PCCC", StringComparison.OrdinalIgnoreCase) >= 0)
                        || (system == MepPipeSystem.Fire && layer.IndexOf("SMOKE", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!onSystemLayer && !(ent is Polyline) && !(ent is BlockReference))
                    {
                        continue;
                    }

                    if (ent is Polyline pl && pl.NumberOfVertices >= 2)
                    {
                        if (!onSystemLayer && pl.NumberOfVertices < 2)
                        {
                            continue;
                        }

                        if (!onSystemLayer)
                        {
                            continue;
                        }

                        var pts = new List<PipePointJson>();
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            Point2d p = pl.GetPoint2dAt(i);
                            pts.Add(new PipePointJson { x = p.X, y = p.Y });
                        }

                        segments.Add(new PipeSegmentJson
                        {
                            points = pts,
                            width = pl.ConstantWidth > 0 ? pl.ConstantWidth : 50,
                            layer = layer
                        });
                    }
                    else if (ent is BlockReference br && onSystemLayer)
                    {
                        fittings.Add(new PipeFittingJson
                        {
                            name = GetBlockName(br, tr),
                            kind = GuessFittingKind(GetBlockName(br, tr)),
                            x = br.Position.X,
                            y = br.Position.Y,
                            rotation_deg = br.Rotation * 180.0 / Math.PI
                        });
                    }
                }

                tr.Commit();
            }

            if (segments.Count == 0 && fittings.Count == 0)
            {
                ed.WriteMessage("\nKhong tim thay polyline/block tren layer " + layerName + ".");
                return null;
            }

            string dwgDir = Path.GetDirectoryName(doc.Name) ?? Path.GetTempPath();
            string jsonPath = Path.Combine(
                dwgDir,
                system == MepPipeSystem.Water ? "mep_water_export.json" : "mep_fire_export.json");

            var payload = new PipeSystemJson
            {
                system = system == MepPipeSystem.Water ? "water" : "fire",
                name = Path.GetFileNameWithoutExtension(doc.Name) ?? labelFor(system),
                title = labelFor(system),
                segments = segments,
                fittings = fittings,
                analysis = BuildDefaultAnalysis(system, segments)
            };

            using (var ms = new MemoryStream())
            {
                var ser = new DataContractJsonSerializer(typeof(PipeSystemJson));
                ser.WriteObject(ms, payload);
                File.WriteAllBytes(jsonPath, ms.ToArray());
            }

            ed.WriteMessage(
                $"\n[MEP] Xuat {segments.Count} doan ong, {fittings.Count} phu kien -> {jsonPath}");
            return jsonPath;
        }

        private static PipeAnalysisJson BuildDefaultAnalysis(MepPipeSystem system, List<PipeSegmentJson> segments)
        {
            double lengthMm = 0;
            foreach (PipeSegmentJson seg in segments)
            {
                if (seg.points == null || seg.points.Count < 2)
                {
                    continue;
                }

                for (int i = 1; i < seg.points.Count; i++)
                {
                    double dx = seg.points[i].x - seg.points[i - 1].x;
                    double dy = seg.points[i].y - seg.points[i - 1].y;
                    lengthMm += Math.Sqrt(dx * dx + dy * dy);
                }
            }

            double lengthM = lengthMm / 1000.0;
            var items = new List<PipeAnalysisItemJson>();

            items.Add(new PipeAnalysisItemJson
            {
                title = "Chieu dai ong (uoc tinh)",
                value = Math.Round(lengthM, 2),
                unit = "m",
                note = system == MepPipeSystem.Water
                    ? "Tong polyline tren layer MEP_WATER"
                    : "Tong polyline tren layer MEP_FIRE"
            });

            if (system == MepPipeSystem.Water)
            {
                MepCalculationResult vel = WaterCalculations.PipeVelocityFromFlowLs(2.5, 50);
                items.Add(new PipeAnalysisItemJson
                {
                    title = vel.Title,
                    value = Math.Round(vel.Value, 3),
                    unit = vel.Unit,
                    note = vel.Note
                });
            }
            else
            {
                MepCalculationResult spr = FireCalculations.SprinklerFlow(80, 1.0);
                items.Add(new PipeAnalysisItemJson
                {
                    title = spr.Title,
                    value = Math.Round(spr.Value, 2),
                    unit = spr.Unit,
                    note = spr.Note
                });
            }

            return new PipeAnalysisJson { items = items };
        }

        private static string labelFor(MepPipeSystem system) =>
            system == MepPipeSystem.Water ? "He nuoc" : "He PCCC";

        private static string GetBlockName(BlockReference br, Transaction tr)
        {
            try
            {
                var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                return btr.Name ?? "FITTING";
            }
            catch
            {
                return "FITTING";
            }
        }

        private static string GuessFittingKind(string blockName)
        {
            string n = (blockName ?? "").ToUpperInvariant();
            if (n.Contains("SPRINK") || n.Contains("PHUN")) return "sprinkler";
            if (n.Contains("HYDR") || n.Contains("CHUA") || n.Contains("HCT")) return "hydrant";
            if (n.Contains("DETEC") || n.Contains("BAO") || n.Contains("SMOKE")) return "detector";
            if (n.Contains("TEE") || n.Contains("_T") || n.Contains("CHU")) return "tee";
            if (n.Contains("VALVE") || n.Contains("VAN")) return "valve";
            if (n.Contains("REDUC") || n.Contains("GIAM")) return "reducer";
            if (n.Contains("CAP") || n.Contains("NUT") || n.Contains("BIT")) return "cap";
            if (n.Contains("ELBOW") || n.Contains("CO") || n.Contains("FIT_ELBOW")) return "elbow90";
            return "fitting";
        }

        private static bool CallPythonRenderer(
            Editor ed,
            MepPipeSystem system,
            string inputPath,
            string outputPath)
        {
            string scriptPath = FindRendererScript();
            if (scriptPath == null)
            {
                ed.WriteMessage("\nKhong tim thay render_pipe_system.py. Chay: .\\scripts\\install-renderer-devices.ps1");
                return false;
            }

            if (!TryResolvePython(out string pythonExe, out string argPrefix))
            {
                ed.WriteMessage("\nKhong tim thay Python 3. Cai tu python.org (tick Add to PATH).");
                return false;
            }

            string sys = system == MepPipeSystem.Water ? "water" : "fire";
            string args = argPrefix + BuildRenderArgs(sys, inputPath, outputPath, scriptPath);
            ed.WriteMessage("\n[MEP] Dang render " + sys + "...");
            string workDir = Path.GetDirectoryName(scriptPath) ?? "";

            try
            {
                var psi = new ProcessStartInfo(pythonExe, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workDir
                };

                using (Process p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(TimeoutMs))
                    {
                        try { p.Kill(); } catch { /* ignore */ }
                        ed.WriteMessage("\n[MEP] Render timeout.");
                        return false;
                    }

                    if (p.ExitCode != 0)
                    {
                        ed.WriteMessage("\n[MEP] Loi render: " + stderr);
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(stdout))
                    {
                        ed.WriteMessage("\n[MEP] " + stdout.Trim());
                    }

                    return File.Exists(outputPath);
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n[MEP] Loi goi renderer: " + ex.Message);
                return false;
            }
        }

        private static string BuildRenderArgs(
            string system,
            string inputPath,
            string outputPath,
            string scriptPath)
        {
            if (inputPath != null)
            {
                return Quote(scriptPath)
                    + " --system " + system
                    + " --input " + Quote(inputPath)
                    + " --output " + Quote(outputPath);
            }

            return Quote(scriptPath)
                + " --system " + system
                + " --demo"
                + " --output " + Quote(outputPath);
        }

        private static string Quote(string path) => "\"" + path + "\"";

        private static bool TryResolvePython(out string exe, out string argPrefix)
        {
            foreach (var spec in new[]
            {
                new { Exe = "py", Args = "-3 " },
                new { Exe = "python", Args = "" },
                new { Exe = "python3", Args = "" }
            })
            {
                try
                {
                    var psi = new ProcessStartInfo(spec.Exe, spec.Args + "--version")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using (Process p = Process.Start(psi))
                    {
                        string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                        p.WaitForExit(5000);
                        if (p.ExitCode == 0
                            && output.IndexOf("Python", StringComparison.OrdinalIgnoreCase) >= 0
                            && output.IndexOf("was not found", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            exe = spec.Exe;
                            argPrefix = spec.Args;
                            return true;
                        }
                    }
                }
                catch
                {
                    /* try next */
                }
            }

            exe = null;
            argPrefix = "";
            return false;
        }

        private static string FindRendererScript()
        {
            string pluginDir = Path.GetDirectoryName(typeof(MepPipeRenderService).Assembly.Location) ?? "";
            string[] candidates =
            {
                Path.Combine(pluginDir, RendererScript),
                Path.Combine(pluginDir, "scripts", RendererScript)
            };

            foreach (string p in candidates)
            {
                string full = Path.GetFullPath(p);
                if (File.Exists(full))
                {
                    return full;
                }
            }

            return null;
        }

        [DataContract]
        private class PipeSystemJson
        {
            [DataMember] public string system { get; set; }
            [DataMember] public string name { get; set; }
            [DataMember] public string title { get; set; }
            [DataMember] public List<PipeSegmentJson> segments { get; set; }
            [DataMember] public List<PipeFittingJson> fittings { get; set; }
            [DataMember] public PipeAnalysisJson analysis { get; set; }
        }

        [DataContract]
        private class PipeSegmentJson
        {
            [DataMember] public List<PipePointJson> points { get; set; }
            [DataMember] public double width { get; set; }
            [DataMember] public string layer { get; set; }
        }

        [DataContract]
        private class PipePointJson
        {
            [DataMember] public double x { get; set; }
            [DataMember] public double y { get; set; }
        }

        [DataContract]
        private class PipeFittingJson
        {
            [DataMember] public string name { get; set; }
            [DataMember] public string kind { get; set; }
            [DataMember] public double x { get; set; }
            [DataMember] public double y { get; set; }
            [DataMember] public double rotation_deg { get; set; }
        }

        [DataContract]
        private class PipeAnalysisJson
        {
            [DataMember] public List<PipeAnalysisItemJson> items { get; set; }
        }

        [DataContract]
        private class PipeAnalysisItemJson
        {
            [DataMember] public string title { get; set; }
            [DataMember] public double value { get; set; }
            [DataMember] public string unit { get; set; }
            [DataMember] public string note { get; set; }
        }
    }
}
