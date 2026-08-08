using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.ApplicationServices;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    /// <summary>
    /// Chay ve CAD an toan tu WinForms palette (tranh eLockViolation).
    /// </summary>
    internal static class MepBlocksDocumentContext
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
                        try
                        {
                            doc.Editor.WriteMessage("\n[MEP] Loi ve CAD: " + ex.Message);
                        }
                        catch
                        {
                        }
                    }
                },
                null);
        }
    }
}
