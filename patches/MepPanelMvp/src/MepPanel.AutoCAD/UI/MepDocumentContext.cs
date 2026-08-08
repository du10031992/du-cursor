using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.ApplicationServices;

namespace MepPanelMvp.UI
{
    /// <summary>
    /// Chay code ve CAD an toan tu cua so WPF modeless.
    /// Application context: ExecuteInCommandContextAsync (khong block UI - tranh deadlock).
    /// Document context: LockDocument truc tiep.
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

            if (!docs.IsApplicationContext)
            {
                using (doc.LockDocument())
                {
                    action();
                }
                return;
            }

            // Quan trong: KHONG Wait() tren UI thread - se deadlock AutoCAD.
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
