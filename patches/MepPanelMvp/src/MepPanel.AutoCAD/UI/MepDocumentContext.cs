using System;
using System.Threading;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.ApplicationServices;

namespace MepPanelMvp.UI
{
    /// <summary>
    /// Chay code ve CAD an toan tu cua so WPF modeless.
    /// Tranh eLockViolation bang ExecuteInCommandContextAsync + LockDocument.
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

            // Dang trong document/command context -> chi can LockDocument.
            if (!docs.IsApplicationContext)
            {
                using (doc.LockDocument())
                {
                    action();
                }
                return;
            }

            // Tu WPF modeless (application context) -> chuyen sang command context.
            Exception caught = null;
            using (var done = new ManualResetEventSlim(false))
            {
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
                            caught = ex;
                        }
                        finally
                        {
                            done.Set();
                        }
                    },
                    null);

                if (!done.Wait(TimeSpan.FromMinutes(10)))
                {
                    throw new TimeoutException("Timeout khi ve CAD (command context).");
                }
            }

            if (caught != null)
            {
                throw caught;
            }
        }
    }
}
