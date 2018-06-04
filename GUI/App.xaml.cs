using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MessagePassingComm;

namespace GUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool testMode { get; set; } = false;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            GUI.MainWindow mwin = new GUI.MainWindow();
            mwin.cleanXmlFile();
            if (e.Args.Length >= 1)
            {
                if (e.Args[0] == "test")
                {
                    testMode = true;
                    bool result = mwin.test();
                    Console.Write("\n  command line test passed");
                }
            }
        }
    }
}
