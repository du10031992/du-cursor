using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.ApplicationServices;

namespace MepPanelMvp.UI
{
    /// <summary>
    /// Ve CAD an toan tu WPF:
    /// - Dang trong command context: chay dong bo (AutoCAD da lock document)
    /// - Dang o application context (modeless UI): ExecuteInCommandContextAsync
    /// Khong queue hoac LockDocument long nhau.
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

            // Da nam trong Run -> command context va document lock da duoc AutoCAD cap.
            // LockDocument lan nua tai day gay nested lock va eLockViolation.
            if (_depth > 0)
            {
                action();
                return;
            }

            // Lenh AutoCAD dang chay: document hien tai da duoc AutoCAD lock.
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

            // Tu panel WPF modeless -> chuyen sang command context duy nhat.
            // Callback da o command context, khong LockDocument thu cong lan nua.
            docs.ExecuteInCommandContextAsync(
                async _ =>
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
