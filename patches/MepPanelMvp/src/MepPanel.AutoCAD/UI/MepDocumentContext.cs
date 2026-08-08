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

            // Da nam trong command/document context -> chay thang.
            if (!docs.IsApplicationContext)
            {
                using (doc.LockDocument())
                {
                    action();
                }
                return;
            }

            // Tu panel WPF modeless -> chuyen sang command context (1 lan).
            docs.ExecuteInCommandContextAsync(
                async _ =>
                {
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
                },
                null);
        }
    }
}
