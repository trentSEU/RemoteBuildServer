/////////////////////////////////////////////////////////////////////////////
//  ChildProc.cs - child process implements the function of building given //
//                 project                                                 //                                                                
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
 *   This package implements the child process which get the request from the
 *   builder and try to build the project, after that the child process would
 *   send back a ready message to the builder and get ready to process another
 *   request. If build success, the child process would send the test libraries
 *   to the tester automatically.
 * 
 * 
 *   Public Interface
 *   ----------------
 *   public void ready()                                        -generate and send a ready message to the bulder
 *   public void ChildLoop()                                    -process the requests from the builder
 *   public void BuildCsproj(string proPath)                    -build the given project and print the results to the console
 *   public void tryToBuild(string buildPath, int size)         -before the files delivered, keep trying to find files in the local path, at most 10 times
 *   public string findFile(string dirPath, string fileName)    -find the file with specific extension in the given path
 *   public void testStubFunction()                             -the test stub fot the ChildProc.cs
 *   
 */
/*
 *   Build Process
 *   -------------
 *   - Required files:   IMessagePassingCommService.cs MessagePassingCommService.cs childProc.cs XmlRequest.cs
 * 
 *   Maintenance History
 *   -------------------
 *   ver 1.0 : 27 October 2017
 *     - first release
 *   ver 2.0 : 6 December 2017
 * 
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using MessagePassingComm;
using System.Threading;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Execution;
using XmlMessage; 

namespace ChildProc
{
    class ChildComm
    {
        private Comm ChdComm;
        private Sender sndToClient;
        private Sender sndToRepo;
        private int maxCount;
        private bool buildSucceed;
        private string portNum;
        private string localAddress;
        private string repoBuildLog;
        private string fileStorage;
        private string chdLogPath;
        public string currentDirPath { get; set; } = "";
        public string pathToTestLibs { get; set; } = "";
        private string chdXmlPath = "";
        public string dir { get; set; } = "";
        public int size { get; set; } = 0;
        public List<string> files { get; set; } = new List<string>();
        public int logCount { get; set; } = 0;

        public ChildComm(string port)
        {
            maxCount = 10;
            portNum = port;
            repoBuildLog = "../../../Repo/BuildLog";
            chdXmlPath = "../../../ChildProc/Xml";
            fileStorage = "..//..//..//ChildProc//" + port;
            chdLogPath = fileStorage + "//Log";
            localAddress = "http://localhost:" + portNum + "/IPluggableComm";
            ChdComm = new Comm("http://localhost", int.Parse(portNum));
            pathToTestLibs = "../../../Tester/bin/Debug/TestLibraries";
            sndToClient = new Sender("http://localhost", int.Parse(portNum)-30);
            sndToRepo = new Sender("http://localhost", int.Parse(portNum) - 50);
            if (Directory.Exists(fileStorage))
                Directory.Delete(fileStorage, true);
            if (!Directory.Exists(chdLogPath))
                Directory.CreateDirectory(chdLogPath);
            Directory.CreateDirectory(fileStorage);
            //cleanFiles(chdXmlPath);
            readyToRepo();
            cleanFiles(chdLogPath);
        }
        /*-------------generate and send ready message to the builder--------------*/

        public void ready()
        {
            CommMessage readyNow = new CommMessage(CommMessage.MessageType.request);
            readyNow.command = "ready";
            readyNow.author = "Yuan Liu";
            readyNow.to = "http://localhost:8081/IPluggableComm";
            readyNow.from = localAddress;
            ChdComm.postMessage(readyNow);
        }
        /*-------------generate and send ready message to the Repo to construct the connect--------------*/

        private void readyToRepo()
        {
            CommMessage readyNow = new CommMessage(CommMessage.MessageType.request);
            readyNow.command = "ready";
            readyNow.author = "Yuan Liu";
            readyNow.to = "http://localhost:8080/IPluggableComm";
            readyNow.from = localAddress;
            sndToRepo.postMessage(readyNow);
        }
        /*-------------builder loop: process the request from the parent process--------------*/

        public void ChildLoop()
        {
            while (true)
            {
                CommMessage msg = ChdComm.getMessage();
                msg.show();
                if(msg.type == CommMessage.MessageType.close)
                {
                    Process pro = Process.GetCurrentProcess();
                    pro.Kill();
                }
                else if (msg.command == "build" && msg.type != CommMessage.MessageType.connect)
                {
                    msg.command = "send xml";
                    msg.to = msg.from;
                    msg.from = localAddress;
                    msg.show();
                    ChdComm.postMessage(msg);
                    checkXml(msg);
                }
                else if(msg.command == "start build")
                {
                    tryToBuild(msg.fileStorage, msg.size);
                }
                else if(msg.command == "send dll")
                {
                    postDllFile(msg);
                }
                else if(msg.command == "tester ready")
                {
                    ready();
                }
            }
        }
        /*-------------build the csproj in the given path--------------*/

        public void BuildCsproj(string proPath, string outpath)
        {
            string logName = portNum+ "-log" + logCount++;
            string projectFileName = proPath;
            buildSucceed = false;

            ConsoleLogger logger = new ConsoleLogger();
            FileLogger Flogger = new FileLogger() { Parameters = @"logfile=" + chdLogPath + "/" + logName, Verbosity = LoggerVerbosity.Normal };

            Dictionary<string, string> GlobalProperty = new Dictionary<string, string>();
            GlobalProperty.Add("Configuration", "Debug");
            GlobalProperty.Add("Platform", "Any CPU");
            GlobalProperty.Add("OutputType", "Library");
            GlobalProperty.Add("OutputPath", outpath);
            BuildRequestData BuildRequest = new BuildRequestData(projectFileName, GlobalProperty, null, new string[] { "Rebuild" }, null);
            BuildParameters bp = new BuildParameters();
            bp.Loggers = new List<ILogger> { logger, Flogger };

            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(bp, BuildRequest);
            if (buildResult.OverallResult == BuildResultCode.Success)
            {
                buildSucceed = true;
                sendTestRequest();
            }
            postLogFile(logName);
            Console.WriteLine();
        }
        /*---------------generate and send the test request to the tester---------------*/

        private void sendTestRequest()
        {
            CommMessage msg = new CommMessage(CommMessage.MessageType.request);
            msg.from = localAddress;
            msg.to = "http://localhost:8078/IPluggableComm";
            msg.author = "Yuan Liu";
            msg.command = "test";
            ChdComm.postMessage(msg);
        }
        /*---------------post dll file to the storage given by the msg---------------*/

        private void postDllFile(CommMessage msg)
        {
            foreach(string file in getDllFileNames(currentDirPath))
            {
                ChdComm.postFile(file, currentDirPath, msg.fileStorage);
            }
        }
        /*---------------post the log file to the repo---------------*/

        private void postLogFile(string logname)
        {
            sndToRepo.postFile(logname, chdLogPath, repoBuildLog);
        }

        /*-------------return the build result to client--------------*/

        public void buildResultMessage(bool success)
        {
            CommMessage msg = new CommMessage(CommMessage.MessageType.request);
            msg.command = "display result";
            msg.author = "Yuan Liu";
            msg.from = localAddress;
            msg.to = "http://localhost:8079/IPluggableComm";
            if (success) msg.content = "true";
            else msg.content = "false";
            sndToClient.postMessage(msg);
        }
        /*-------------try to build the project at most maxCount times--------------*/

        public void tryToBuild(string buildPath, int size)
        {
            int tryCount = 0;
            string[] files = Directory.GetFiles(buildPath);
            while (files.Count() < size)
            {
                files = Directory.GetFiles(buildPath);
                if (tryCount++ == maxCount)
                {
                    Console.Write("\n  Time out, please try to debug again \n");
                    return;
                }
                Console.Write("\n  tried {0} times \n", tryCount);
                Thread.Sleep(500);
            }
            string csprojPath = findFile(buildPath,"csproj");
            if(csprojPath != "")
            {
                try { BuildCsproj(csprojPath, buildPath); }
                catch (Exception e) { Console.Write(e.Message); };
                if (buildSucceed)
                {
                    Console.Write("\nbuild SUCCEED!\n");
                }
                else
                {
                    Console.Write("\nbuild FAILED!\n");
                    ready();
                }
            }
            else
            {
                Console.Write("\nbuild FAILED - can't find the csproj file\n");
                ready();
                return;
            }
        }
        /*-------------return the the path of the csproj in the given dir--------------*/

        public string findFile(string dirPath, string fileName)
        {
            string[] files = Directory.GetFiles(dirPath);
            foreach(string name in files)
            {
                string filePath = Path.GetFullPath(name);
                if(filePath.Substring(filePath.LastIndexOf(".") + 1).Equals(fileName))
                {
                    Console.Write("\nthe Path found is: {0}\n", name);
                    return name;
                }
            }
            return "";
        }
        /*---------------the test stub fot the ChildProc.cs---------------*/

        public void testStubFunction()
        {
            Console.Write("\n try to test the build function of the child process");
            Console.Write("\n ---------------------------------------------------\n");
            tryToBuild(@"../../../Repo/RepoStorage/test1", 4);
        }
        /*---------------clean all of the files in the given path---------------*/

        private void cleanFiles(string cleanPath)
        {
            string[] xmlFiles = Directory.GetFiles(cleanPath);
            foreach (string file in xmlFiles)
                File.Delete(file);
        }
        /*---------------check whether the Xml file has been sent or not, 
         * if yes, ask for files in the Xml---------------*/

        private void checkXml(CommMessage msg)
        {
            int tryCount = 0;
            while (!getFileNames(chdXmlPath).Contains(msg.xmlName))
            {
                List<string> files = getFileNames(chdXmlPath);
                if (tryCount++ == maxCount)
                {
                    Console.Write("\n  Time out, please try to debug again \n");
                    return;
                }
                Console.Write("\n  tried {0} times \n", tryCount);
                Thread.Sleep(500);
            }
            try { parseXml(msg.xmlName); }
            catch (Exception e) { Console.Write("\n {0} \n", e.Message); }
            string dirPath = Path.Combine(fileStorage, msg.xmlName);
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            currentDirPath = dirPath;
            msg.type = CommMessage.MessageType.reply;
            msg.command = "send file";
            msg.to = "http://localhost:8080/IPluggableComm";
            msg.from = localAddress;
            msg.dirName = dir;
            msg.size = size;
            msg.arguments = files;
            msg.fileStorage = Path.GetFullPath(dirPath);
            msg.show();
            ChdComm.postMessage(msg);
        }
        /*---------------parse the xml file---------------*/

        private void parseXml(string xmlname)
        {
            string xmlPath = Path.Combine(chdXmlPath, xmlname);
            XmlRequest xmlmsg = new XmlRequest();
            xmlmsg.loadXml(xmlPath);
            dir = xmlmsg.parse("dir");
            size = int.Parse((xmlmsg.parse("size")));
            files = xmlmsg.parseList("file");
        }
        /*---------------return a list contains the names for the files in the given path---------------*/

        private List<string> getFileNames(string path)
        {
            List<string> fileNames = new List<string>();
            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
                fileNames.Add(Path.GetFileName(file));
            return fileNames;
        }
        /*---------------return a list contains the names for the dll files in the given path----------------*/

        private List<string> getDllFileNames(string path)
        {
            List<string> fileNames = new List<string>();
            string[] files = Directory.GetFiles(path,"*dll");
            foreach (string file in files)
                fileNames.Add(Path.GetFileName(file));
            return fileNames;
        }
    }
    class Childproc
    {
        static void Main(string[] args)
        {
            Console.Title = "ChildProc";

            Console.Write("\n  Demo Child Process");
            Console.Write("\n ====================");

            if (args.Count() == 0)
            {
#if (TEST_CHILDPROC)
                ChildComm comm = new ChildComm("8082");
                comm.testStubFunction();
                return;
#endif
                Console.Write("\n  please enter integer value on command line");
                return;
            }
            else
            {
                ChildComm comm = new ChildComm(args[0]);
                Console.Write("\n  Hello from child, the port number is {0}\n\n", args[0]);
                comm.ready();
                comm.ChildLoop();
            }
            Console.Write("\n  Press key to exit");
            
        }
    }
}
