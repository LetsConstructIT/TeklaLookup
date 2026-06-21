// Tekla Lookup — launcher macro.
//
// Installed to BOTH <env>\macros\modeling\ and <env>\macros\drawings\ by the
// TSEP (see installer\Manifest.xml), this is what makes "Tekla Lookup" appear in
// Tekla's Applications & components catalog in both the model editor and the
// drawing editor. Running it starts the standalone TeklaLookup.exe (shipped in
// <env>\extensions\TeklaLookup\), which connects to the open model/drawing over
// the Tekla Open API and shows the WPF inspector UI. The launcher is identical
// in both contexts, so one source file serves both targets.
//
// The catalog thumbnail is "Tekla Lookup.png" (96x96) next to this file: Tekla
// pairs a macro with a same-named PNG in the macros folder automatically.

using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Tekla.Structures;

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            string dataDir = "";
            TeklaStructuresSettings.GetAdvancedOption("XSDATADIR", ref dataDir);

            string appName = "TeklaLookup.exe";
            string appPath = Path.Combine(dataDir,
                "Environments\\common\\extensions\\TeklaLookup\\" + appName);

            if (File.Exists(appPath))
            {
                try
                {
                    // Fire-and-forget (no WaitForExit): Tekla Lookup is a modeless
                    // inspector window the user keeps open while working in Tekla.
                    Process.Start(appPath);
                }
                catch
                {
                    MessageBox.Show(appName + " failed to start.");
                }
            }
            else
            {
                MessageBox.Show(appName + " not found.");
            }
        }
    }
}
