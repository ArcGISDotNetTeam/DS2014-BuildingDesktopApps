using Esri.ArcGISRuntime;
using System.Windows;

namespace DesktopCreateMap
{
    public partial class App : Application
    {
        public App()
        {
            // Initialize runtime. This is place where to set ClientId and/or licenses.
            ArcGISRuntimeEnvironment.Initialize();
        }
    }
}
