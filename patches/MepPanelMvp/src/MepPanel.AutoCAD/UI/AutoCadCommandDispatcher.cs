using System;
using System.Collections.Generic;
using System.Reflection;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using MepPanel.AutoCAD.Licensing;
using MepPanel.Core;

namespace MepPanelMvp.UI
{
    /// <summary>
    /// Goi chuc nang tu panel WPF (MepInternalCommand) — khong can lenh CLI.
    /// Su dung mot command context duy nhat de tranh eLockViolation tu modeless WPF.
    /// </summary>
    internal static class AutoCadCommandDispatcher
    {
        private static readonly object Sync = new object();
        private static Dictionary<string, MethodInvoker> _map;

        private sealed class MethodInvoker
        {
            public Type Type { get; set; }
            public MethodInfo Method { get; set; }
            public string FeatureCode { get; set; }
        }

        public static void Queue(string command)
        {
            string cmd = (command ?? string.Empty).Trim();
            if (cmd.Length == 0)
            {
                return;
            }

            if (!EnsureCommandFeature(cmd))
            {
                return;
            }

            // Vao command context 1 LAN qua MepDocumentContext.
            // Cac lenh ben trong (HvacCommands/PanelCommands) neu goi MepDocumentContext.Run
            // se thay _depth > 0 -> chay dong bo, KHONG long ExecuteInCommandContextAsync
            // -> tranh eLockViolation.
            try
            {
                MepDocumentContext.Run(() => InvokeCommand(cmd));
            }
            catch (Exception ex)
            {
                // Khong invoke truc tiep tu WPF: do chinh la duong gay eLockViolation.
                WriteMessage("[MEP] Khong the chay " + cmd + " trong document context: " + ex.Message);
            }
        }

        private static void InvokeCommand(string commandName)
        {
            if (!TryInvoke(commandName))
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                doc?.SendStringToExecute(commandName + " ", true, false, true);
            }
        }

        private static bool TryInvoke(string commandName)
        {
            EnsureMap();
            if (!_map.TryGetValue(commandName, out MethodInvoker invoker) || invoker == null)
            {
                return false;
            }

            try
            {
                object instance = invoker.Method.IsStatic
                    ? null
                    : Activator.CreateInstance(invoker.Type);
                invoker.Method.Invoke(instance, null);
                return true;
            }
            catch (TargetInvocationException ex)
            {
                string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                WriteMessage("[MEP] Loi " + commandName + ": " + msg);
                return true;
            }
            catch (Exception ex)
            {
                WriteMessage("[MEP] Loi goi " + commandName + ": " + ex.Message);
                return true;
            }
        }

        private static bool EnsureCommandFeature(string commandName)
        {
            EnsureMap();

            MethodInvoker invoker;
            string feature = _map.TryGetValue(commandName, out invoker) && invoker != null
                ? invoker.FeatureCode
                : null;

            if (string.IsNullOrWhiteSpace(feature))
            {
                feature = CommandFeatureRegistry.Resolve(commandName);
            }

            if (!string.IsNullOrWhiteSpace(feature))
            {
                return LicenseGuard.EnsureSubFeature(feature);
            }

            // Chi MEPDB la entry CLI. Lenh noi bo MEP moi ma chua khai bao
            // feature phai bi chan (fail closed), tranh bo sot quyen khi nang cap.
            if (commandName.StartsWith("MEP", StringComparison.OrdinalIgnoreCase))
            {
                WriteMessage(
                    "[MEP] Lenh " + commandName +
                    " chua dang ky feature. Hay them [MepRequiresFeature] hoac CommandFeatureRegistry.");
                return false;
            }

            return true;
        }

        private static void WriteMessage(string text)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage("\n" + text);
        }

        private static void EnsureMap()
        {
            if (_map != null)
            {
                return;
            }

            lock (Sync)
            {
                if (_map != null)
                {
                    return;
                }

                var map = new Dictionary<string, MethodInvoker>(StringComparer.OrdinalIgnoreCase);
                Assembly asm = typeof(AutoCadCommandDispatcher).Assembly;

                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types ?? Array.Empty<Type>();
                }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract)
                    {
                        continue;
                    }

                    foreach (MethodInfo method in type.GetMethods(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        if (method.GetParameters().Length != 0)
                        {
                            continue;
                        }

                        foreach (string name in GetCommandNames(method))
                        {
                            if (string.IsNullOrWhiteSpace(name))
                            {
                                continue;
                            }

                            if (string.Equals(name, "MEPDB", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            map[name] = new MethodInvoker
                            {
                                Type = type,
                                Method = method,
                                FeatureCode = GetRequiredFeature(method) ?? CommandFeatureRegistry.Resolve(name)
                            };
                        }
                    }
                }

                TryMapStatic(map, "MepPanelMvp.Commands.WaterFireCommands", "ShowWaterMenu", "MEPWATER");
                TryMapStatic(map, "MepPanelMvp.Commands.WaterFireCommands", "ShowFireMenu", "MEPFIRE");

                _map = map;
            }
        }

        private static void TryMapStatic(
            Dictionary<string, MethodInvoker> map,
            string typeName,
            string methodName,
            string commandName)
        {
            try
            {
                Type t = typeof(AutoCadCommandDispatcher).Assembly.GetType(typeName);
                if (t == null)
                {
                    return;
                }

                MethodInfo mi = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (mi == null)
                {
                    return;
                }

                map[commandName] = new MethodInvoker
                {
                    Type = t,
                    Method = mi,
                    FeatureCode = GetRequiredFeature(mi) ?? CommandFeatureRegistry.Resolve(commandName)
                };
            }
            catch
            {
            }
        }

        private static string GetRequiredFeature(MethodInfo method)
        {
            foreach (object attribute in method.GetCustomAttributes(false))
            {
                var required = attribute as MepRequiresFeatureAttribute;
                if (required != null && !string.IsNullOrWhiteSpace(required.FeatureCode))
                {
                    return required.FeatureCode;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetCommandNames(MethodInfo method)
        {
            foreach (object attr in method.GetCustomAttributes(false))
            {
                Type at = attr.GetType();
                if (at.Name == "MepInternalCommandAttribute")
                {
                    PropertyInfo p = at.GetProperty("Name");
                    object v = p != null ? p.GetValue(attr, null) : null;
                    if (v != null)
                    {
                        yield return v.ToString();
                    }
                }
                else if (at.Name == "CommandMethodAttribute")
                {
                    foreach (string propName in new[] { "GlobalName", "LocalizedName", "GroupName" })
                    {
                        PropertyInfo p = at.GetProperty(propName);
                        if (p == null)
                        {
                            continue;
                        }

                        object v = p.GetValue(attr, null);
                        if (v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                        {
                            yield return v.ToString();
                        }
                    }

                    FieldInfo[] fields = at.GetFields(
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    foreach (FieldInfo f in fields)
                    {
                        if (f.FieldType != typeof(string))
                        {
                            continue;
                        }

                        object v = f.GetValue(attr);
                        string s = v as string;
                        if (!string.IsNullOrWhiteSpace(s) && s.StartsWith("MEP", StringComparison.OrdinalIgnoreCase))
                        {
                            yield return s;
                        }
                    }
                }
            }
        }
    }
}
