/////////////////////////////////////////////////////////////////////////////
//  MainWindow.xaml.cs - implements the GUI for the project                //                                                              
//  Language:     C#, VS 2017                                              //
//  Platform:     MacBook Pro, Windows 10                                  //
//  Application:  Demonstration for CSE681 - Software Modeling & Analysis  //
//  Author:       Yuan Liu, Syracuse University                            //
//                (315) 870-8079, yliu219@syr.edu                          //
//  Reference:    Jim Fawcett                                              //
/////////////////////////////////////////////////////////////////////////////
/*
 *   Package Operations
 *   ------------------
 *   This package implements the GUI for the project, the GUI could show all
 *   of the project files saved in the repo 
 *   -Select the menu by double click the menu name in the Menu listbox.
 *   -Click the Top button to back to the root directory.
 *   -Select the files for building by double click the file name in the
 *    files listbox, selected files would listed in the selected files listbox.
 *   -Click the Clear button to clear the seleted files and reselect.
 *   -Click the BuildXml button to build the xml file. 
 *   -Input the number of process to start in the textbox, the number should
 *    between 0 and 20, by default, the number is 1.
 *   -Click the start button to start the specific number of process, if there
 *    is no valid input in the textbox, the GUI would start 1 child process by
 *    default.
 *   -Double click the Xml file name listed in the Xml listbox to build it.
 *   -Click the Build-Test button to start building the project, the result would 
 *    be printed in the console.
 *   -Click the close button to close all of the processes.
 *   
 *   NOTE:
 *   ----
 *   In order to test the function of the GUI automatically, please set command line
 *   arguments as test and set GUI as the startup project. For manually test, just
 *   set the GUI as the startup project without setting the command line arguments.
 * 
 * 
 *   Public Interface
 *   ----------------
 *   public partial class MainWindow:
 *   public void getMenu()                                        -get the menu consistd of files to be built in the loacl repo
 *   public void displayMenu()                                    -display the file menu get from the local repo
 *   public void cleanXmlFile()                                   -Clear the Xml files in the storage
 *   
 */
/*
 *   Build Process
 *   -------------
 *   - Required files:   IMessagePassingCommService.cs MessagePassingCommService.cs MainWindow.xaml.cs XmlRequest.cs
 * 
 *   Maintenance History
 *   -------------------
 *   ver 1.0 : 27 October 2017
 *     - first release
 *   ver 2.0 : 6 December 2017
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MessagePassingComm;
using System.Diagnostics;
using System.Threading;

namespace GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string commandLine { get; set; } = "1";
        private const string repoPath = @"..\..\..\Repo\RepoStorage\";
        private const string GUIXmlPath = @"..\..\Xml\";
        private const string AutoXmlPath = @"..\..\AutoTest\";
        private const string repoXmlPath = @"../../../Repo/Xml";
        private const string repoLocation = "..//..//..//Repo//bin//Debug//Repo.exe";
        private const string builderLocation = "..//..//..//builder//bin//Debug//builder.exe";
        private const string testerLocation = "..//..//..//Tester//bin//Debug//Tester.exe";
        public string xmlName { get; set; } = "";
        public List<string> Menus { get; set; } = null;
        public List<string> selectedPaths { get; set; } = null;
        public List<string> XmlFiles { get; set; } = null;
        public Dictionary<string, List<string>> dirFiles { get; set; } = null;
        public int xmlCount { get; set; } = 0;
        public bool processOn { get; set; } = false;
        public int dirCount { get; set; } = 0;
        public bool success { get; set; } = false;
        private Comm GUIComm;
        private Dictionary<string, Action<CommMessage>> messageDispatcher = new Dictionary<string, Action<CommMessage>>();
        Thread rcvThread = null;

        public MainWindow()
        {
            dirFiles = new Dictionary<string, List<string>>();
            selectedPaths = new List<string>();
            Menus = new List<string>();
            XmlFiles = new List<string>();
            InitializeComponent();
            getMenu();
            displayMenu();
            ButtonBuild.IsEnabled = false;
            ButtonClose.IsEnabled = false;
            ButtonBuildXml.IsEnabled = false;
            if (App.testMode)
            {
                ButtonStart.IsEnabled = false;
                ButtonTop.IsEnabled = false;
                ButtonBuildXml.IsEnabled = false;
                ButtonClear.IsEnabled = false;
            }
            initializeMessageDispatcher();
        }
        /*---------thread to process the received message---------*/
        private void rcvThreadProc()
        {
            while (true)
            {
                CommMessage msg = GUIComm.getMessage();
                if (msg.command == null)
                    continue;

                // pass the Dispatcher's action value to the main thread for execution

                Dispatcher.Invoke(messageDispatcher[msg.command], new object[] { msg });
            }
        }
        /*---------initialize Message Dispatcher with lambda---------*/

        private void initializeMessageDispatcher()
        {
            // load remoteFiles listbox with files from root

            messageDispatcher["display result"] = (CommMessage msg) =>
            {
                if(msg.content == "true")
                    output.Items.Add("build succeed!");
                else
                    output.Items.Add("build failed!");
            };

            messageDispatcher["send xml"] = (CommMessage msg) =>
            {
                GUIComm.postFile(msg.xmlName, GUIXmlPath, repoXmlPath);
            };
        }
        /*---------get the files to be built in the loacl repo---------*/

        public void getMenu()
        {
            string[] files = Directory.GetDirectories(repoPath);
            foreach (string file in files)
            {
                Menus.Add(file);
                Menu.Items.Add(file);
            }

        }
        /*---------display the file menu get from the local repo---------*/

        public void displayMenu()
        {
            Menu.Items.Clear();
            foreach (string name in Menus)
            {
                string[] pathElement = name.Split(new char[] { '\\' });
                string dirname = pathElement[pathElement.Length - 1];
                Menu.Items.Add(dirname);
            }
        }
        /*---------the event handler for the button Start---------*/

        private void StartProcess(object sender, RoutedEventArgs e)
        {
            if(processOn)
            {
                output.Items.Add("Process already started!");
                return;
            }
            GUIComm = new Comm("http://localhost", 8079);
            rcvThread = new Thread(rcvThreadProc);
            rcvThread.Start();
            processOn = true;
            ButtonClose.IsEnabled = true;
            ButtonBuildXml.IsEnabled = true;
            commandLine = textbox1.GetLineText(0);
            if (commandLine == "between 0-20, default 1" || commandLine == "")
                commandLine = "1";
            else if (!(0 < int.Parse(commandLine) && int.Parse(commandLine) < 20)) commandLine = "1";
            string repoFile = System.IO.Path.GetFullPath(repoLocation);
            string builderFile = System.IO.Path.GetFullPath(builderLocation);
            string testerFile = System.IO.Path.GetFullPath(testerLocation);

            output.Items.Add("attempting to start Builder");
            output.Items.Add("attempting to start Repo");
            output.Items.Add("started " + commandLine + " child process");
            try
            {
                Process.Start(builderFile, commandLine);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
            try
            {
                Process.Start(repoFile);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
            try
            {
                Process.Start(testerFile);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
            ButtonStart.IsEnabled = false;
        }
        /*---------the event handler for the button Build-Test---------*/

        private void BuildFile(object sender, RoutedEventArgs e)
        {
            buildRequest(xmlName);
            ButtonBuild.IsEnabled = false;
        }
        /*---------generate and send a message and xml file for building request---------*/

        private void buildRequest(string xmlname)
        {
            CommMessage buildReq = new CommMessage(CommMessage.MessageType.request);
            buildReq.command = "clientBuild";
            buildReq.author = "Yuan Liu";
            buildReq.xmlName = xmlname;
            buildReq.to = "http://localhost:8080/IPluggableComm";
            buildReq.from = "http://localhost:8079/IPluggableComm";
            GUIComm.postMessage(buildReq);
        }
        /*---------send a close message to the repo and close the comm for the GUI---------*/

        private void closeBuilder(object sender, RoutedEventArgs e)
        {
            CommMessage msg = new CommMessage(CommMessage.MessageType.close);
            msg.from = "http://localhost:8079/IPluggableComm";
            msg.to = "http://localhost:8080/IPluggableComm";
            GUIComm.postMessage(msg);
            CommMessage msg1 = new CommMessage(CommMessage.MessageType.closeReceiver);
            msg1.from = "http://localhost:8079/IPluggableComm";
            msg1.to = "http://localhost:8079/IPluggableComm";
            GUIComm.postMessage(msg1);
            CommMessage msg2 = new CommMessage(CommMessage.MessageType.closeSender);
            msg2.from = "http://localhost:8079/IPluggableComm";
            msg2.to = "http://localhost:8079/IPluggableComm";
            GUIComm.postMessage(msg2);
            output.Items.Add("GUI closed receiver");
            output.Items.Add("GUI closed sender");
            ButtonClose.IsEnabled = false;
        }
        /*---------displaying the tips for the textbox---------*/

        private void textbox1_MouseEnter(object sender, EventArgs e)
        {
            if(textbox1.Text == "between 0-20, default 1")
                textbox1.Text = "";
        }
        /*----------display back to the repoStorage menu-----------*/

        private void GetTopMenu(object sender, RoutedEventArgs e)
        {
            Menu.Items.Clear();
            Files.Items.Clear();
            SelectedFiles.Items.Clear();
            XmlFiles.Clear();
            displayMenu();
        }
        /*----------clear the listed files in the Selected Files listbox-----------*/

        private void ClearSelectedFiles(object sender, RoutedEventArgs e)
        {
            SelectedFiles.Items.Clear();
            XmlFiles.Clear();
        }
        /*----------Build a xml file with the files listed in the Selected Files list box-----------*/

        private void BuildXml(object sender, RoutedEventArgs e)
        {
            int size = 0;
            string dirname, filename, xmlname;
            xmlname = "Xml" + xmlCount++;
            string xmlPath = System.IO.Path.Combine(GUIXmlPath, xmlname);
            XmlMessage.XmlRequest buildReq= new XmlMessage.XmlRequest();
            foreach (string file in XmlFiles)
            {
                size++;
                string[] pathElement = file.Split(new char[] { '\\' });
                dirname = pathElement[pathElement.Length - 2];
                filename = pathElement[pathElement.Length - 1];
                buildReq.files.Add(filename);
                buildReq.dir = dirname;
                buildReq.size = size+"";
            }
            buildReq.makeRequest();
            buildReq.saveXml(xmlPath);
            xmlFiles.Items.Add(xmlname);
        }
        /*----------select a menu to show in the Files listbox-----------*/

        private void Menu_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ButtonBuild.IsEnabled = false;
            Files.Items.Clear();
            string dirName = Menu.SelectedValue as string;
            getFiles(dirName);
            Menu.Items.Clear();
            Menu.Items.Add(dirName);

        }
        /*----------select files to show in the Selected Files listbox-----------*/

        private void Files_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ButtonBuild.IsEnabled = false;
            string fileName = Files.SelectedValue as string;
            if(!SelectedFiles.Items.Contains(fileName))
            {
                SelectedFiles.Items.Add(fileName);
                XmlFiles.Add(fileName);
            }
        }
        /*----------select a Xml file to be built-----------*/

        private void XML_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (processOn) ButtonBuild.IsEnabled = true;
            xmlName = xmlFiles.SelectedValue as string;
            output.Items.Add(xmlName + " selected, let's build it!");
            xmlFiles.Items.Remove(xmlFiles.SelectedValue);
        }
        /*----------display the file name in Files after double-click menu in Menu----------*/

        private void getFiles(string dir)
        {
            string path = System.IO.Path.Combine(repoPath, dir);
            string[] files = Directory.GetFiles(path);
            foreach(string file in files)
            {
                string[] pathElement = file.Split(new char[] { '\\' });
                string dirname = pathElement[pathElement.Length - 2];
                string filename = pathElement[pathElement.Length - 1];
                string dirpath = System.IO.Path.Combine(dirname, filename);
                Files.Items.Add(dirpath);
            }
        }
        /*----------Clear the Xml files in the storage-----------*/

        public void cleanXmlFile()
        {
            string[] xmlFiles = Directory.GetFiles(GUIXmlPath);
            foreach (string file in xmlFiles)
                File.Delete(file);
        }
        /*----------function for auto test-----------*/

        public bool test()
        {
            xmlCount = 6;
            GUIComm = new Comm("http://localhost", 8079);
            rcvThread = new Thread(rcvThreadProc);
            rcvThread.Start();
            commandLine = "3";
            string repoFile = System.IO.Path.GetFullPath(repoLocation);
            string builderFile = System.IO.Path.GetFullPath(builderLocation);
            string testerFile = System.IO.Path.GetFullPath(testerLocation);
            try
            {
                Process.Start(builderFile, commandLine);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
            try
            {
                Process.Start(repoFile);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
            try
            {
                Process.Start(testerFile);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
            ButtonStart.IsEnabled = false;
            string[] autoXmlFiles = Directory.GetFiles(AutoXmlPath);
            foreach (string file in autoXmlFiles)
            {
                string autoFileName = System.IO.Path.GetFileName(file);
                string fileFrom = System.IO.Path.Combine(AutoXmlPath, autoFileName);
                //string fileFromPath = System.IO.Path.GetFullPath(fileFrom);
                string fileTo = System.IO.Path.Combine(GUIXmlPath, autoFileName);
                //string fileToPath = System.IO.Path.GetFullPath(fileTo);
                Console.Write("\n Generate XML: {0}", autoFileName);
                File.Copy(fileFrom, fileTo);
                buildRequest(autoFileName);
                Console.Write("\n begin building {0}, test the dll if success", autoFileName);
                Thread.Sleep(150);
            }
            return true;
        }
    }
}
