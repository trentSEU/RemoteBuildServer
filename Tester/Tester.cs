/////////////////////////////////////////////////////////////////////
// Tester.cs - Test Harness Test Aggregator                        //
// ver 1.1                                                         //
//                                                                 //
// Platform:    Dell Dimension 8300, Windows XP Pro, SP 2.0        //
// Application: CSE784 - Software Studio, Final Project Prototype  //
// Author:      Jim Fawcett, Syracuse University, CST 2-187        //
//              jfawcett@twcny.rr.com, (315) 443-3948              //
/////////////////////////////////////////////////////////////////////
/*
 * Module Operations:
 * ==================
 * This module provides operations to Create a child Application Domain,
 * load libraries into it, and run tests on all loaded libraries that
 * support the ITest interface. 
 * 
 * In order to load libraries without requiring the Tester to bind to
 * the types they declare, a Loader library is defined that is loaded
 * into the child domain, and loads each of the test libraries from
 * within the child. 
 * 
 * Test configurations are defined by the set of all libraries found
 * in a configuration directory.  Each configuration runs on its own
 * thread.  Test results are returned as a private XML string.
 * 
 * Public Interface:
 * =================
 * Tester tstr = new Tester();
 * Thread t = tstr.SelectConfigAndRun("TestLibraries");
 * tstr.ShowTestResults();
 * tstr.UnloadTestDomain();
 */
/*
 * Build Process:
 * ==============
 * Files Required:
 *   Tester.cs, Loader.cs, ITest.cs, Test1.cs, Tested1.cs, ...
 * Compiler Command:
 *   csc /t:Library Loader.cs
 *   csc /t:Library ITest.cs
 *   csc /t:Library Test1.cs, Tested1.cs, ...
 *   csc /t:exe     Tester.cs
 * Deployment:
 *   Loader.dll --> TesterDir/Loader
 *   ITest.dll  --> TesterDir/TestLibraries
 *   Test dlls  --> TesterDir/TestLibraries
 *   where TesterDir is the folder containing Tester.exe
 *  
 * Maintence History:
 * ==================
 * ver 1.1 : 17 Oct 05
 *   - uses new version of Loader.cs.  Otherwise unchanged.
 * ver 1.0 : 09 Oct 05
 *   - first release
 * ver 2.0 : 06 Dec 17
 * 
 */
//
using System;
using System.IO;
using System.Xml;
using System.Windows.Forms;
using System.Collections;
using System.Security.Policy;
using System.Reflection;
using System.Runtime.Remoting;
using System.Threading;
using System.Collections.Generic;
using MessagePassingComm;
using System.Diagnostics;

namespace TestHarness
{
    public class Tester
    {
        private string pathToTestLibs_ = "../../../Tester/bin/Debug/TestLibraries";  // ITest.dll and test libs
        private string localAddress = "http://localhost:8078/IPluggableComm";
        private string fileStorage = "../../../Tester/TestLibraries";
        private string currentFileStorage = "";
        private int logCount = 0;
        public string logName { get; set; } ="";
        public string logPath { get; set; } = "../../../Tester/Log";
        private const string repoLogPath = "../../../Repo/TestLog";
        private bool pass = false;
        AppDomain ad;
        string results_;
        Comm testerComm;
        private const int maxCount = 10;
        

        //----< Constructor placeholder >--------------------------------

        public Tester()
        {
            testerComm = new Comm("http://localhost", 8078);
            cleanFiles(pathToTestLibs_);
            cleanFiles(logPath);
            if (Directory.Exists(fileStorage))
                Directory.Delete(fileStorage, true);
        }
        //----< loop to get message and execute the corresponding operation >-----------------

        private void testerLoop()
        {
            Console.Write("The test path is: {0}", pathToTestLibs_);
            while (true)
            {
                CommMessage msg = testerComm.getMessage();
                msg.show();
                if(msg.command == "test")
                {
                    string dirName = "TestLog" + (logCount++);
                    currentFileStorage = Path.Combine(fileStorage, dirName);
                    if (!Directory.Exists(currentFileStorage))
                        Directory.CreateDirectory(currentFileStorage);
                    logName = dirName + ".txt";
                    msg.fileStorage = currentFileStorage;
                    msg.to = msg.from;
                    msg.from = localAddress;
                    msg.command = "send dll";
                    testerComm.postMessage(msg);
                    checkDll();
                    postLog();
                    postReadyMsg(msg);
                }
                else if(msg.command == "close")
                {
                    Process pro = Process.GetCurrentProcess();
                    pro.Kill();
                }
            }
        }
        //----< post ready message to the child process >-----------------

        private void postReadyMsg(CommMessage msg)
        {
            CommMessage readyMsg = new CommMessage(CommMessage.MessageType.reply);
            readyMsg.from = msg.from;
            readyMsg.to = msg.to;
            readyMsg.command = "tester ready";
            testerComm.postMessage(readyMsg);
        }
        //----< send the log to the repo >-----------------

        private void postLog()
        {
            testerComm.postFile(logName, logPath, repoLogPath);
        }
        //----< check loacl dll storage to find the dll to be built >-----------------

        private void checkDll()
        {
            int tryCount = 0;
            while (getFileNames(currentFileStorage).Count == 0)
            {
                List<string> files = getFileNames(currentFileStorage);
                if (tryCount++ == maxCount)
                {
                    Console.Write("\n  Time out, please try to debug again \n");
                    return;
                }
                Console.Write("\n  tried {0} times \n", tryCount);
                Thread.Sleep(500);
            }
            Thread t = SelectConfigAndRun(currentFileStorage);
            t.Join();
            ShowTestResults();
            UnloadTestDomain();
        }
        //----< Create AppDomain in which to run tests >-----------------

        private List<string> getFileNames(string path)
        {
            List<string> fileNames = new List<string>();
            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
                fileNames.Add(Path.GetFileName(file));
            return fileNames;
        }
        //----< clean files in the given path >-----------------

        private void cleanFiles(string cleanPath)
        {
            string[] xmlFiles = Directory.GetFiles(cleanPath);
            foreach (string file in xmlFiles)
                File.Delete(file);
        }

        //----< Create AppDomain in which to run tests >-----------------

        void CreateAppDomain()
        {
            AppDomainSetup domainInfo = new AppDomainSetup();
            domainInfo.ApplicationName = "TestDomain";
            Evidence evidence = AppDomain.CurrentDomain.Evidence;
            ad = AppDomain.CreateDomain("TestDomain", evidence, domainInfo);
        }
        //----< Load Loader and tests, run tests, unload AppDomain >-----

        void LoadAndRun()
        {
            Console.Write("\n\n  Loading and instantiating Loader in TestDomain");
            Console.Write("\n ------------------------------------------------");
            ad.Load("Loader");
            ObjectHandle oh = ad.CreateInstance("loader", "TestHarness.Loader");
            Loader ldr = oh.Unwrap() as Loader;
            ldr.SetPath(currentFileStorage);
            ldr.LoadTests();
            //results_ = ldr.RunTests(logName, logPath);
        }
        //
        //----< Run tests in configDir >---------------------------------

        void runTests()
        {
            try
            {
                CreateAppDomain();
                LoadAndRun();
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
            Console.Write("\n");
        }
        //----< unload Child AppDomain >---------------------------------

        void UnloadTestDomain()
        {
            AppDomain.Unload(ad);
        }
        //
        //----< show test results >--------------------------------------

        void ShowTestResults()
        {
            Console.Write("\n  Test Results returned to Tester");
            Console.Write("\n ---------------------------------\n");

            Console.Write("\n  {0}\n", results_);
            StringReader tr = new StringReader(results_);
            XmlTextReader xtr = new XmlTextReader(tr);
            xtr.MoveToContent();
            if (xtr.Name != "TestResults")
                throw new Exception("invalid test results: " + results_);
            int count = 0;
            string name = "", text = "";
            while (xtr.Read())
            {
                if (xtr.NodeType == XmlNodeType.Element)
                    name = xtr.Name;
                if (xtr.NodeType == XmlNodeType.Text)
                {
                    if (xtr.Value == "True")
                    {
                        text = "passed";
                        pass = true;
                        if (pass) Console.Write("");
                    }
                    else
                    {
                        text = "failed";
                    }
                    ++count;
                    Console.Write("\n  Test #{0}: {1} - {2}", count, text, name);
                }
            }
            Console.Write("\n\n");
        }
        //
        //----< run configuration on its own thread >--------------------

        Thread SelectConfigAndRun(string configDir)
        {
            pathToTestLibs_ = configDir;
            Thread t = new Thread(new ThreadStart(this.runTests));
            t.Start();
            return t;
        }
        //----< demonstrate Test Harness Prototype >---------------------

        static void Main(string[] args)
        {
            Console.Title = "TestHarness";
            Console.Write(
              "\n  Tester, ver 1.1 - Demonstrates Prototype TestHarness"
            );
            
            Console.Write(
              "\n ======================================================"
            );
            Tester tstr = new Tester();
#if (TEST_TESTER)
            tstr.currentFileStorage = "../../testStub";
            tstr.SelectConfigAndRun(tstr.currentFileStorage);
            return;
#endif
            tstr.testerLoop();
        }
    }
}
