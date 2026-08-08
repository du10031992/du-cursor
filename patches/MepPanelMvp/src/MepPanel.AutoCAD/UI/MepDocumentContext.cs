using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.ApplicationServices;

namespace MepPanelMvp.UI
{
    /// <summary>
    /// Chay code ve CAD an toan tu WPF modeless.
    /// LUON ExecuteInCommandContextAsync. Khong LockDocument tren UI thread.
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
                        ShowError(doc, ex);
                    }
                },
                null);
        }

        private static void ShowError(Document doc, Exception ex)
        {
            try
            {
                AcApp.ShowAlertDialog("Loi ve CAD: " + ex.Message);
            }
            catch
            {
                try
                {
                    doc.Editor.WriteMessage("\n[MEP] Loi ve CAD: " + ex.Message);
                }
                catch
                {
                }
            }
        }
    }
}
