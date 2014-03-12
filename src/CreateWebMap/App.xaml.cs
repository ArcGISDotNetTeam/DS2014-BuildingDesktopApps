using Esri.ArcGISRuntime;
using System.Windows;

namespace CreateWebMap
{
    public partial class App : Application
    {
        public App()
        {
            // Initialize runtime. This is place where to set ClientId and/or licenses.
            ArcGISRuntimeEnvironment.Initialize();

			DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

		private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
		{
			MessageBox.Show(e.Exception.ToString());
		}
    }
}
