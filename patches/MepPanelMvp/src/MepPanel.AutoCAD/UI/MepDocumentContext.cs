using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.ApplicationServices;

namespace MepPanelMvp.UI
{
    /// <summary>
    /// Ve CAD an toan tu WPF:
    /// - Dang trong command context: LockDocument + chay dong bo
    /// - Dang o application context (modeless UI): ExecuteInCommandContextAsync
    /// Khong bao gio queue long nhau (tranh mat chuc nang / prompt).
    /// </summary>
    public static class MepDocumentContext
    {
        [ThreadStatic]
        private static int _depth;

        public static void Run(Action action)
        {
            if (action == null)
            {
                return;
            }

            DocumentCollection docs = AcApp.DocumentManager;
            Document doc = docs.MdiActiveDocument;
            if (doc == null)
            {
                throw new InvalidOperationException("Khong co ban ve AutoCAD dang mo.");
            }

            // Da nam trong Run/command context -> khong queue long (giu feature).
            if (_depth > 0 || !docs.IsApplicationContext)
            {
                _depth++;
                try
                {
                    using (doc.LockDocument())
                    {
                        action();
                    }
                }
                finally
                {
                    _depth--;
                }
                return;
            }

            // Tu panel WPF modeless -> chuyen sang command context (1 lan).
            docs.ExecuteInCommandContextAsync(
                async _ =>
                {
                    _depth++;
                    try
                    {
                        using (doc.LockDocument())
                        {
                            action();
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            AcApp.ShowAlertDialog("Loi ve CAD: " + ex.Message);
                        }
                        catch
                        {
                            doc.Editor.WriteMessage("\n[MEP] Loi ve CAD: " + ex.Message);
                        }
                    }
                    finally
                    {
                        _depth--;
                    }
                },
                null);
        }
    }
}
