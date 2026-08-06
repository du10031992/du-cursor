using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    public static class MepCabinetRenderService
    {
        private const string RendererScript = "render_cabinet.py";
        private const int TimeoutBlenderMs = 600000;
        private const int TimeoutFastMs = 120000;

        public static void RenderFromDrawing()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            string quality = PromptQuality(ed);
            if (quality == null) return;

            var kw = new PromptKeywordOptions("\nNguon du lieu [TuBanVe] TuFile Demo") { AllowNone = true };
            kw.Keywords.Add("TuBanVe");
            kw.Keywords.Add("TuFile");
            kw.Keywords.Add("Demo");
            kw.Keywords.Default = "TuBanVe";

            PromptResult mode = ed.GetKeywords(kw);
            string src = mode.Status == PromptStatus.OK ? mode.StringResult : "TuBanVe";

            string jsonPath = null;
            switch (src)
            {
                case "Demo": break;
                case "TuFile":
                    var filePr = ed.GetString(new PromptStringOptions("\nDuong dan file CSV/JSON: ") { AllowSpaces = true });
                    if (filePr.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(filePr.StringResult)) return;
                    jsonPath = filePr.StringResult.Trim();
                    if (!File.Exists(jsonPath)) { ed.WriteMessage("\nKhong tim thay file: " + jsonPath); return; }
                    break;
                default:
                    jsonPath = ExtractFromDrawing(doc, ed);
                    if (jsonPath == null) { ed.WriteMessage("\nKhong doc duoc du lieu tu ban ve."); return; }
                    break;
            }

            string outPng = Path.Combine(Path.GetTempPath(), "MEP_CABINET_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            if (!CallPythonRenderer(ed, jsonPath, outPng, quality)) return;

            ed.WriteMessage("\n[MEP] Render thanh cong: " + outPng);
            try { Process.Start(new ProcessStartInfo(outPng) { UseShellExecute = true }); }
            catch { ed.WriteMessage("\nFile: " + outPng); }
        }

        private static string PromptQuality(Editor ed)
        {
            var kw = new PromptKeywordOptions("\nChe do render [Blender3D] Photoreal Nhanh") { AllowNone = true };
            kw.Keywords.Add("Blender3D");
            kw.Keywords.Add("Photoreal");
            kw.Keywords.Add("Nhanh");
            kw.Keywords.Default = "Blender3D";
            var pr = ed.GetKeywords(kw);
            if (pr.Status == PromptStatus.Cancel) return null;
            switch (pr.Status == PromptStatus.OK ? pr.StringResult : "Blender3D")
            {
                case "Photoreal": return "photoreal";
                case "Nhanh": return "standard";
                default: return "blender";
            }
        }

        private static string ExtractFromDrawing(Autodesk.AutoCAD.ApplicationServices.Document doc, Editor ed)
        {
            var devices = new List<CabinetDeviceJson>();
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;
                    if (ent is BlockReference br && br.HasAttributes)
                    {
                        var dev = ReadBlockAttribs(br, tr);
                        if (dev != null) devices.Add(dev);
                    }
                }
                tr.Commit();
            }
            if (devices.Count == 0) { ed.WriteMessage("\nKhong tim thay block thiet bi."); return null; }
            return WriteJsonFile(devices, doc);
        }

        private static CabinetDeviceJson ReadBlockAttribs(BlockReference br, Transaction tr)
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId attId in br.AttributeCollection)
            {
                if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference att)
                    attrs[att.Tag.Trim()] = att.TextString.Trim();
            }
            if (!attrs.ContainsKey("DEVICE_TYPE") && !attrs.ContainsKey("TYPE")) return null;
            string dtype = attrs.TryGetValue("DEVICE_TYPE", out var dt) ? dt : attrs["TYPE"];
            return new CabinetDeviceJson
            {
                name = attrs.TryGetValue("NAME", out var n) ? n : dtype,
                type = dtype,
                in_a = ParseDouble(attrs, "IN_A", "IN"),
                poles = (int)ParseDouble(attrs, "POLES", "P"),
                qty = Math.Max(1, (int)ParseDouble(attrs, "QTY", "SL"))
            };
        }

        private static string WriteJsonFile(List<CabinetDeviceJson> devices, Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            string jsonPath = Path.Combine(Path.GetDirectoryName(doc.Name) ?? Path.GetTempPath(), "mep_cabinet_export.json");
            var cab = new CabinetJson
            {
                name = Path.GetFileNameWithoutExtension(doc.Name),
                type = "Tu phan phoi",
                size = "H600xW500xD225",
                bays = new List<BayJson> { new BayJson { name = "NGAN 1", label = "Phan phoi", devices = devices } }
            };
            using (var ms = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(CabinetJson[])).WriteObject(ms, new[] { cab });
                File.WriteAllBytes(jsonPath, ms.ToArray());
            }
            return jsonPath;
        }

        private static bool CallPythonRenderer(Editor ed, string inputPath, string outputPath, string quality)
        {
            string scriptPath = FindRendererScript();
            if (scriptPath == null)
            {
                ed.WriteMessage("\nKhong tim thay render_cabinet.py. Chay install-renderer-devices.ps1");
                return false;
            }
            if (!TryResolvePython(out string pythonExe, out string argPrefix))
            {
                ed.WriteMessage("\nKhong tim thay Python 3.");
                return false;
            }
            if (quality == "blender" && FindBlender() == null)
                ed.WriteMessage("\n[MEP] Blender chua cai - fallback Pillow photoreal.");

            string args = argPrefix + BuildRenderArgs(inputPath, outputPath, quality, scriptPath);
            ed.WriteMessage(quality == "blender"
                ? "\n[MEP] Dang render Blender 3D... co the mat 2-5 phut."
                : "\n[MEP] Dang render (" + quality + ")...");

            int timeout = quality == "blender" ? TimeoutBlenderMs : TimeoutFastMs;
            try
            {
                var psi = new ProcessStartInfo(pythonExe, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? ""
                };
                using (var p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(timeout)) { try { p.Kill(); } catch { } ed.WriteMessage("\n[MEP] Timeout."); return false; }
                    if (p.ExitCode != 0) { ed.WriteMessage("\n[MEP] Loi: " + stderr); return false; }
                    if (!string.IsNullOrWhiteSpace(stdout)) ed.WriteMessage("\n[MEP] " + stdout.Trim());
                    return File.Exists(outputPath);
                }
            }
            catch (Exception ex) { ed.WriteMessage("\n[MEP] " + ex.Message); return false; }
        }

        private static string BuildRenderArgs(string inputPath, string outputPath, string quality, string scriptPath)
        {
            var parts = new List<string> { Quote(scriptPath), "--mode", "interior", "--quality", quality, "--output", Quote(outputPath) };
            if (inputPath != null) { parts.Add("--input"); parts.Add(Quote(inputPath)); } else parts.Add("--demo");
            if (quality == "blender") { parts.Add("--engine"); parts.Add("cycles"); parts.Add("--samples"); parts.Add("256"); }
            return string.Join(" ", parts);
        }

        private static string Quote(string p) => "\"" + p + "\"";

        private static bool TryResolvePython(out string exe, out string prefix)
        {
            foreach (var s in new[] { new { E = "py", P = "-3 " }, new { E = "python3", P = "" }, new { E = "python", P = "" } })
            {
                try
                {
                    using (var p = Process.Start(new ProcessStartInfo(s.E, s.P + "--version") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }))
                    {
                        p.WaitForExit(5000);
                        if (p.ExitCode == 0) { exe = s.E; prefix = s.P; return true; }
                    }
                }
                catch { }
            }
            exe = null; prefix = ""; return false;
        }

        private static string FindRendererScript()
        {
            string dir = Path.GetDirectoryName(typeof(MepCabinetRenderService).Assembly.Location) ?? "";
            foreach (string rel in new[] { RendererScript, Path.Combine("scripts", RendererScript) })
            {
                string full = Path.GetFullPath(Path.Combine(dir, rel));
                if (File.Exists(full)) return full;
            }
            return null;
        }

        private static string FindBlender()
        {
            foreach (string n in new[] { "blender", "blender.exe" })
            {
                try
                {
                    using (var p = Process.Start(new ProcessStartInfo(n, "--version") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }))
                    { p.WaitForExit(5000); if (p.ExitCode == 0) return n; }
                }
                catch { }
            }
            string bf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation");
            if (Directory.Exists(bf))
                foreach (string d in Directory.GetDirectories(bf))
                    if (File.Exists(Path.Combine(d, "blender.exe"))) return Path.Combine(d, "blender.exe");
            return null;
        }

        private static double ParseDouble(Dictionary<string, string> d, params string[] keys)
        {
            foreach (var k in keys)
                if (d.TryGetValue(k, out var v) && double.TryParse(v, out var r)) return r;
            return 0;
        }

        [DataContract] private class CabinetJson { [DataMember] public string name { get; set; } [DataMember] public string type { get; set; } [DataMember] public string size { get; set; } = "H600xW500xD225"; [DataMember] public List<BayJson> bays { get; set; } }
        [DataContract] private class BayJson { [DataMember] public string name { get; set; } [DataMember] public string label { get; set; } [DataMember] public List<CabinetDeviceJson> devices { get; set; } }
        [DataContract] private class CabinetDeviceJson { [DataMember] public string name { get; set; } [DataMember] public string type { get; set; } [DataMember] public double in_a { get; set; } [DataMember] public int poles { get; set; } = 1; [DataMember] public int qty { get; set; } = 1; }
    }
}
