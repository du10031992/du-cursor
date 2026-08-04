using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(MepPanel.LoadTest.LoadTestCommands))]

namespace MepPanel.LoadTest
{
    public class LoadTestCommands
    {
        [CommandMethod("MEPPING", CommandFlags.Modal)]
        public void Ping()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage("\nMepPanel.LoadTest OK — NETLOAD hoạt động.");
        }
    }
}
