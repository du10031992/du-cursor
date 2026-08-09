using System;
using System.Threading.Tasks;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.ApplicationServices;

namespace MepPanel.Blocks.AutoCAD.Drawing
{
    /// <summary>
    /// Ve CAD an toan tu WinForms palette.
    /// </summary>
    internal static class MepBlocksDocumentContext
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

            if (_depth > 0)
            {
                action();
                return;
            }

            if (!docs.IsApplicationContext)
            {
                _depth++;
                try
                {
                    action();
                }
                finally
                {
                    _depth--;
                }
                return;
            }

            docs.ExecuteInCommandContextAsync(
                _ =>
                {
                    _depth++;
                    try
                    {
                        action();
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
                    finally
                    {
                        _depth--;
                    }
                    return Task.CompletedTask;
                },
                null);
        }
    }
}
