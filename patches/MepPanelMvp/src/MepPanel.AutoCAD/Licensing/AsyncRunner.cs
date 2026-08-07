using System;
using System.Threading.Tasks;

namespace MepPanel.AutoCAD.Licensing
{
    /// <summary>
    /// Tranh deadlock khi goi HttpClient async tu luong AutoCAD/UI.
    /// </summary>
    internal static class AsyncRunner
    {
        public static T Run<T>(Func<Task<T>> work)
        {
            return Task.Run(work).GetAwaiter().GetResult();
        }

        public static void Run(Func<Task> work)
        {
            Task.Run(work).GetAwaiter().GetResult();
        }
    }
}
