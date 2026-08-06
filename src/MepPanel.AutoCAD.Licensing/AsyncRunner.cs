using System;
using System.Threading.Tasks;

namespace MepPanel.AutoCAD.Licensing
{
    /// <summary>
    /// Tránh deadlock khi gọi HttpClient async từ luồng AutoCAD/UI.
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
