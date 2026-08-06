using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    /// <summary>
    /// Đọc dữ liệu thiết bị từ bản vẽ AutoCAD (block attributes / TEXT)
    /// → xuất JSON → gọi render_cabinet.py → trả về đường dẫn PNG.
    /// </summary>
    public static class MepCabinetRenderService
    {
        private const string RendererScript = "render_cabinet.py";
        private const string DefaultOutputName = "MEP_CABINET_RENDER.png";

        // ─── Entry point từ panel ─────────────────────────────────────────
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
                    jsonPath = null;
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
                        ed.WriteMessage($"\nKhong tim thay file: {jsonPath}");
                        return;
                    }
                    break;

                default:
                    jsonPath = ExtractFromDrawing(doc, ed);
                    if (jsonPath == null)
                    {
                        ed.WriteMessage("\nKhong doc duoc du lieu tu ban ve. Dung '--demo' hoac '--input <file>'.");
                        return;
                    }
                    break;
            }

            string outPng = Path.Combine(
                Path.GetTempPath(),
                $"MEP_CABINET_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            bool ok = CallPythonRenderer(ed, jsonPath, outPng);
            if (!ok)
            {
                return;
            }

            ed.WriteMessage($"\n[MEP] Render thanh cong: {outPng}");

            // Mở ảnh bằng viewer mặc định (Windows Photos / Paint)
            try
            {
                Process.Start(new ProcessStartInfo(outPng) { UseShellExecute = true });
            }
            catch
            {
                ed.WriteMessage("\nKhong mo duoc anh tu dong. File: " + outPng);
            }
        }

        // ─── Đọc block attributes từ bản vẽ ─────────────────────────────
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

                    if (ent is BlockReference br && br.HasAttributes)
                    {
                        var dev = ReadBlockAttribs(br, tr);
                        if (dev != null)
                        {
                            devices.Add(dev);
                        }
                    }

                    if (ent is MText mtext)
                    {
                        var dev = ParseMTextDevice(mtext.Contents);
                        if (dev != null)
                        {
                            devices.Add(dev);
                        }
                    }
                }

                tr.Commit();
            }

            if (devices.Count == 0)
            {
                ed.WriteMessage("\nKhong tim thay block thiet bi tren ban ve (can co attribute DEVICE_TYPE, IN_A).");
                return null;
            }

            return WriteJsonFile(devices, doc);
        }

        private static CabinetDeviceJson ReadBlockAttribs(BlockReference br, Transaction tr)
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var attColl = br.AttributeCollection;
            foreach (ObjectId attId in attColl)
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

            string dtype = attrs.TryGetValue("DEVICE_TYPE", out var dt) ? dt
                         : attrs.TryGetValue("TYPE", out var t) ? t
                         : attrs.TryGetValue("LOAI", out var l) ? l : "";

            return new CabinetDeviceJson
            {
                name = attrs.TryGetValue("NAME", out var n) ? n : attrs.TryGetValue("TEN", out var tn) ? tn : dtype,
                type = dtype,
                in_a = ParseDouble(attrs, "IN_A", "IN", "DONG"),
                poles = (int)ParseDouble(attrs, "POLES", "PHA", "P"),
                qty = Math.Max(1, (int)ParseDouble(attrs, "QTY", "SL", "SO_LUONG")),
                manufacturer = attrs.TryGetValue("MANUFACTURER", out var mfr) ? mfr : attrs.TryGetValue("HANG", out var h) ? h : "",
                note = attrs.TryGetValue("NOTE", out var note) ? note : attrs.TryGetValue("GHI_CHU", out var gc) ? gc : ""
            };
        }

        private static CabinetDeviceJson ParseMTextDevice(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string clean = System.Text.RegularExpressions.Regex.Replace(text, @"\\[A-Za-z][^;]*;|[{}]", "").Trim();
            if (clean.Length < 3)
            {
                return null;
            }

            string upper = clean.ToUpperInvariant();
            bool isMcb = upper.Contains("MCB") || upper.Contains("MCCB") || upper.Contains("ELCB");
            bool isCont = upper.Contains("CONTACTOR") || upper.Contains("CONT");
            bool isMeter = upper.Contains("METER") || upper.Contains("DONG HO") || upper.Contains("ĐỒNG HỒ");

            if (!isMcb && !isCont && !isMeter)
            {
                return null;
            }

            var dev = new CabinetDeviceJson { name = clean.Length > 30 ? clean.Substring(0, 30) : clean, qty = 1 };
            if (isMcb)
            {
                dev.type = clean.ToUpperInvariant().Contains("3P") ? "MCB 3P" : "MCB 1P";
                dev.poles = dev.type.Contains("3P") ? 3 : 1;
                var match = System.Text.RegularExpressions.Regex.Match(clean, @"(\d+)\s*[Aa]");
                if (match.Success)
                {
                    dev.in_a = double.Parse(match.Groups[1].Value);
                }
            }
            else if (isCont)
            {
                dev.type = "CONTACTOR";
                dev.poles = 3;
            }
            else
            {
                dev.type = "METER";
            }

            return dev;
        }

        private static string WriteJsonFile(List<CabinetDeviceJson> devices, Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            string dwgDir = Path.GetDirectoryName(doc.Name) ?? Path.GetTempPath();
            string jsonPath = Path.Combine(dwgDir, "mep_cabinet_export.json");

            var cab = new CabinetJson
            {
                name = Path.GetFileNameWithoutExtension(doc.Name),
                type = "Tủ phân phối",
                bays = new List<BayJson>
                {
                    new BayJson { name = "NGĂN 1", label = "Phân phối", devices = devices }
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

        // ─── Gọi Python renderer ─────────────────────────────────────────
        private static bool CallPythonRenderer(Editor ed, string inputPath, string outputPath)
        {
            string scriptPath = FindRendererScript();
            if (scriptPath == null)
            {
                ed.WriteMessage("\nKhong tim thay render_cabinet.py. Dat vao scripts/ hoac cung thu muc plugin.");
                return false;
            }

            string python = FindPython();
            if (python == null)
            {
                ed.WriteMessage("\nKhong tim thay Python. Cai Python 3 va Pillow.");
                return false;
            }

            string args = inputPath != null
                ? $"\"{scriptPath}\" --input \"{inputPath}\" --output \"{outputPath}\""
                : $"\"{scriptPath}\" --demo --output \"{outputPath}\"";

            ed.WriteMessage($"\n[MEP] Dang render... ({python})");

            try
            {
                var psi = new ProcessStartInfo(python, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (Process p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit(30000);
                    if (p.ExitCode != 0)
                    {
                        ed.WriteMessage("\n[MEP] Loi render: " + stderr);
                        return false;
                    }

                    ed.WriteMessage("\n[MEP] " + stdout.Trim());
                    return File.Exists(outputPath);
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n[MEP] Loi goi renderer: " + ex.Message);
                return false;
            }
        }

        private static string FindRendererScript()
        {
            string pluginDir = Path.GetDirectoryName(typeof(MepCabinetRenderService).Assembly.Location) ?? "";
            string[] candidates =
            {
                Path.Combine(pluginDir, "scripts", RendererScript),
                Path.Combine(pluginDir, RendererScript),
                Path.Combine(pluginDir, "..", "..", "scripts", RendererScript),
                Path.Combine(pluginDir, "..", "scripts", RendererScript)
            };

            foreach (string p in candidates)
            {
                if (File.Exists(Path.GetFullPath(p)))
                {
                    return Path.GetFullPath(p);
                }
            }

            return null;
        }

        private static string FindPython()
        {
            foreach (string name in new[] { "python3", "python", "python3.exe", "python.exe" })
            {
                try
                {
                    var psi = new ProcessStartInfo(name, "--version")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using (Process p = Process.Start(psi))
                    {
                        p.WaitForExit(3000);
                        if (p.ExitCode == 0)
                        {
                            return name;
                        }
                    }
                }
                catch { /* not found */ }
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

        // ─── DTO ─────────────────────────────────────────────────────────
        [DataContract]
        private class CabinetJson
        {
            [DataMember] public string name { get; set; }
            [DataMember] public string type { get; set; }
            [DataMember] public string size { get; set; } = "600x800x200";
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
