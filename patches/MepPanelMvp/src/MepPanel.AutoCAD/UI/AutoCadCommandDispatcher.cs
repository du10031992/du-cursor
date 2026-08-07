using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace MepPanelMvp.UI
{
    /// <summary>
    /// Goi chuc nang tu panel WPF.
    /// Uu tien invoke method noi bo (MepInternalCommand / CommandMethod) — khong can lenh CLI.
    /// </summary>
    internal static class AutoCadCommandDispatcher
    {
        private static readonly object Sync = new object();
        private static Dictionary<string, MethodInvoker> _map;

        private sealed class MethodInvoker
        {
            public Type Type { get; set; }
            public MethodInfo Method { get; set; }
        }

        public static void Queue(string command)
        {
            string cmd = (command ?? string.Empty).Trim();
            if (cmd.Length == 0)
            {
                return;
            }

            if (TryInvoke(cmd))
            {
                return;
            }

            // Fallback: chi khi method van con [CommandMethod] tren CLI (vd MEPDB).
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                doc.SendStringToExecute(cmd + " ", true, false, true);
            }
        }

        private static bool TryInvoke(string commandName)
        {
            EnsureMap();
            MethodInvoker invoker;
            if (!_map.TryGetValue(commandName, out invoker) || invoker == null)
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
                Document doc = Application.DocumentManager.MdiActiveDocument;
                doc?.Editor.WriteMessage("\n[MEP] Loi " + commandName + ": " + msg);
                return true;
            }
            catch (Exception ex)
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                doc?.Editor.WriteMessage("\n[MEP] Loi goi " + commandName + ": " + ex.Message);
                return true;
            }
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

                            // Khong map MEPDB qua dispatcher noi bo — de CLI/login xu ly.
                            if (string.Equals(name, "MEPDB", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            map[name] = new MethodInvoker { Type = type, Method = method };
                        }
                    }
                }

                // Water/Fire static entry (khong can instance CommandMethod)
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

                // Reuse MethodInvoker with static invoke via wrapper type sentinel: Method.IsStatic
                map[commandName] = new MethodInvoker { Type = t, Method = mi };
            }
            catch
            {
                /* ignore */
            }
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
                    // Autodesk.AutoCAD.Runtime.CommandMethodAttribute
                    PropertyInfo global = at.GetProperty("GlobalName")
                        ?? at.GetProperty("GroupName");
                    // Constructor stores localized/global — try common props
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

                    // Fallback: ToString sometimes includes name
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
