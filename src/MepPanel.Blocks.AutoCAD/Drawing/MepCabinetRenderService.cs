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
    /// <summary>
    /// Render tu dien bang Python/Pillow (photoreal) — khong dung Blender.
    /// </summary>
    public static class MepCabinetRenderService
    {
        private const string RendererScript = "render_cabinet.py";
        private const int TimeoutMs = 120000;

        public static void RenderFromDrawing()
        {
            var doc = MepDrawingHelper.GetActiveDocument();
            Editor ed = doc.Editor;

            var kw = new PromptKeywordOptions(
                "\nRender tu dien: nguon du lieu [TuBanVe] TuFile Demo")
            {
                AllowNone = true
            };
            kw.Keywords.Add("TuBanVe");
            kw.Keywords.Add("TuFile");
            kw.Keywords.Add("Demo");
            kw.Keywords.Default = "TuBanVe";

            PromptResult mode = ed.GetKeywords(kw);
            string src = mode.Status == PromptStatus.OK ? mode.StringResult : "TuBanVe";

            string jsonPath = null;
            switch (src)
            {
                case "Demo":
                    break;
                case "TuFile":
                    PromptResult filePr = ed.GetString(new PromptStringOptions(
                        "\nDuong dan file CSV/JSON: ") { AllowSpaces = true });
                    if (filePr.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(filePr.StringResult))
                    {
                        return;
                    }

                    jsonPath = filePr.StringResult.Trim();
                    if (!File.Exists(jsonPath))
                    {
                        ed.WriteMessage("\nKhong tim thay file: " + jsonPath);
                        return;
                    }
                    break;
                default:
                    jsonPath = ExtractFromDrawing(doc, ed);
                    if (jsonPath == null)
                    {
                        ed.WriteMessage("\nKhong doc duoc du lieu tu ban ve. Chon Demo hoac TuFile.");
                        return;
                    }
                    break;
            }

            string outPng = Path.Combine(
                Path.GetTempPath(),
                "MEP_CABINET_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");

            if (!CallPythonRenderer(ed, jsonPath, outPng))
            {
                return;
            }

            ed.WriteMessage("\n[MEP] Render thanh cong: " + outPng);
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
            Editor ed)
        {
            var devices = new List<CabinetDeviceJson>();

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

                    BlockReference br = ent as BlockReference;
                    if (br == null || br.AttributeCollection == null || br.AttributeCollection.Count == 0)
                    {
                        continue;
                    }

                    CabinetDeviceJson dev = ReadBlockAttribs(br, tr);
                    if (dev != null)
                    {
                        devices.Add(dev);
                    }
                }

                tr.Commit();
            }

            if (devices.Count == 0)
            {
                ed.WriteMessage("\nKhong tim thay block thiet bi (can attribute DEVICE_TYPE / IN_A).");
                return null;
            }

            return WriteJsonFile(devices, doc);
        }

        private static CabinetDeviceJson ReadBlockAttribs(BlockReference br, Transaction tr)
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId attId in br.AttributeCollection)
            {
                var attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                if (attRef != null)
                {
                    attrs[attRef.Tag.Trim()] = attRef.TextString.Trim();
                }
            }

            if (!attrs.ContainsKey("DEVICE_TYPE") && !attrs.ContainsKey("TYPE") && !attrs.ContainsKey("LOAI"))
            {
                return null;
            }

            string dtype = attrs.TryGetValue("DEVICE_TYPE", out string dt) ? dt
                         : attrs.TryGetValue("TYPE", out string t) ? t
                         : attrs.TryGetValue("LOAI", out string l) ? l : "";

            return new CabinetDeviceJson
            {
                name = attrs.TryGetValue("NAME", out string n) ? n : attrs.TryGetValue("TEN", out string tn) ? tn : dtype,
                type = dtype,
                in_a = ParseDouble(attrs, "IN_A", "IN", "DONG"),
                poles = (int)ParseDouble(attrs, "POLES", "PHA", "P"),
                qty = Math.Max(1, (int)ParseDouble(attrs, "QTY", "SL", "SO_LUONG")),
                manufacturer = attrs.TryGetValue("MANUFACTURER", out string mfr) ? mfr : "",
                note = attrs.TryGetValue("NOTE", out string note) ? note : ""
            };
        }

        private static string WriteJsonFile(List<CabinetDeviceJson> devices, Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            string dwgDir = Path.GetDirectoryName(doc.Name) ?? Path.GetTempPath();
            string jsonPath = Path.Combine(dwgDir, "mep_cabinet_export.json");

            var cab = new CabinetJson
            {
                name = Path.GetFileNameWithoutExtension(doc.Name),
                type = "Tu phan phoi",
                size = "H600xW500xD225",
                bays = new List<BayJson>
                {
                    new BayJson { name = "NGAN 1", label = "Phan phoi", devices = devices }
                }
            };

            using (MemoryStream ms = new MemoryStream())
            {
                var ser = new DataContractJsonSerializer(typeof(CabinetJson[]));
                ser.WriteObject(ms, new[] { cab });
                File.WriteAllBytes(jsonPath, ms.ToArray());
            }

            return jsonPath;
        }

        private static bool CallPythonRenderer(Editor ed, string inputPath, string outputPath)
        {
            string scriptPath = FindRendererScript();
            if (scriptPath == null)
            {
                ed.WriteMessage("\nKhong tim thay render_cabinet.py. Chay: .\\scripts\\install-renderer-devices.ps1");
                return false;
            }

            if (!TryResolvePython(out string pythonExe, out string argPrefix))
            {
                ed.WriteMessage("\nKhong tim thay Python 3. Cai tu python.org (tick Add to PATH).");
                return false;
            }

            string args = argPrefix + BuildRenderArgs(inputPath, outputPath, scriptPath);
            ed.WriteMessage("\n[MEP] Dang render...");
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

        private static string BuildRenderArgs(string inputPath, string outputPath, string scriptPath)
        {
            // Render goc: Pillow photoreal (khong Blender)
            if (inputPath != null)
            {
                return Quote(scriptPath)
                    + " --mode interior --quality photoreal"
                    + " --input " + Quote(inputPath)
                    + " --output " + Quote(outputPath);
            }

            return Quote(scriptPath)
                + " --mode interior --quality photoreal --demo"
                + " --output " + Quote(outputPath);
        }

        private static string Quote(string path)
        {
            return "\"" + path + "\"";
        }

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
                        string output = (p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd());
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
            string pluginDir = Path.GetDirectoryName(typeof(MepCabinetRenderService).Assembly.Location) ?? "";
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

        private static double ParseDouble(Dictionary<string, string> d, params string[] keys)
        {
            foreach (string k in keys)
            {
                if (d.TryGetValue(k, out string v) && double.TryParse(v, out double result))
                {
                    return result;
                }
            }

            return 0;
        }

        [DataContract]
        private class CabinetJson
        {
            [DataMember] public string name { get; set; }
            [DataMember] public string type { get; set; }
            [DataMember] public string size { get; set; } = "H600xW500xD225";
            [DataMember] public string floor { get; set; } = "";
            [DataMember] public List<BayJson> bays { get; set; }
        }

        [DataContract]
        private class BayJson
        {
            [DataMember] public string name { get; set; }
            [DataMember] public string label { get; set; }
            [DataMember] public List<CabinetDeviceJson> devices { get; set; }
        }

        [DataContract]
        private class CabinetDeviceJson
        {
            [DataMember] public string name { get; set; }
            [DataMember] public string type { get; set; }
            [DataMember] public double in_a { get; set; }
            [DataMember] public int poles { get; set; } = 1;
            [DataMember] public int qty { get; set; } = 1;
            [DataMember] public string manufacturer { get; set; } = "";
            [DataMember] public string note { get; set; } = "";
        }
    }
}
