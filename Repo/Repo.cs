/////////////////////////////////////////////////////////////////////////////
//  Repo.cs       find the file to be built and generate build request to  //
//                the builder                                              //        
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
 *   Get the request from the GUI and verify the information in the message
 *   than send it to the Builder.
 *   For autotest, the repo would generate and send build request automatically.
 *   
 * 
 * 
 *   Public Interface
 *   ----------------
 *   public void sendTestReq()                                          -generate and send build request automatically
 *   public CommMessage startBuild(CommMessage msg)                     -generate and send the start build message after getting the send file message
 *   public void repoLoop()                                             -continue getting and processing the message got
 *   public void buildRequest(string dirName, List<string> fileNames)   -before the files delivered, keep trying to find files in the local path, at most 10 times
 *   public string findFile(string dirPath, string fileName)            -generate and send build request to the Builder
 *   
 */
/*
 *   Build Process
 *   -------------
 *   - Required files:   IMessagePassingCommService.cs MessagePassingCommService.cs Repo.cs XmlRequest.cs
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
using SWTools;
using MessagePassingComm;
using System.Diagnostics;
using System.Threading;

namespace Repo
{
    class Repo
    {
        private const string dirFrom = "../../../Repo/RepoStorage";
        private const string repoXmlPath = "../../../Repo/Xml";
        private const string chldXmlPath = "../../../ChildProc/Xml";
        private const string repoBuildLog = "../../../Repo/BuildLog";
        private const string repoTestLog = "../../../Repo/TestLog";
        public string fileTo { get; set; } = "";
        public string fileFrom { get; set; } = "";
        private const int maxCount = 10;
        private List<string> selectedFiles = null;
        private Comm RepoComm;

        public Repo()
        {
            RepoComm = new Comm("http://localhost", 8080);
            selectedFiles = new List<string>();
            cleanFiles(repoXmlPath);
            cleanFiles(repoBuildLog);
            cleanFiles(repoTestLog);
        }
        /*-------generate and send the start build message after getting the send file message--------*/

        public CommMessage startBuild(CommMessage msg)
        {
            msg.command = "start build";
            msg.to = msg.from;
            msg.from = "http://localhost:8080/IPluggableComm";
            return msg;
        }
        /*-------continue getting and processing the message got--------*/

        public void repoLoop()
        {
            while(true)
            {
                CommMessage msg = RepoComm.getMessage();
                msg.show();
                if (msg.command == "send xml" && msg.type != CommMessage.MessageType.connect)
                {
                    RepoComm.postFile(msg.xmlName, repoXmlPath, chldXmlPath);
                }
                if (msg.command == "send file" && msg.type != CommMessage.MessageType.connect)
                {
                    fileTo = msg.fileStorage;
                    fileFrom = Path.Combine(dirFrom, msg.dirName);
                    foreach (string name in msg.arguments)
                    {
                        bool transferSuccess = RepoComm.postFile(name, fileFrom, fileTo);
                    }
                    RepoComm.postMessage(startBuild(msg));
                }
                if (msg.command == "clientBuild" && msg.type != CommMessage.MessageType.connect)
                {
                    msg.command = "send xml";
                    msg.to = msg.from;
                    msg.from = "http://localhost:8080/IPluggableComm";
                    RepoComm.postMessage(msg);
                    msg.show();
                    checkXml(msg);
                }
                if (msg.type == CommMessage.MessageType.close)
                {
                    CommMessage closemsg = new CommMessage(CommMessage.MessageType.close);
                    closemsg.from = "http://localhost:8080/IPluggableComm";
                    closemsg.to = "http://localhost:8081/IPluggableComm";
                    RepoComm.postMessage(closemsg);
                    CommMessage closemsg1 = new CommMessage(CommMessage.MessageType.closeReceiver);
                    closemsg1.from = "http://localhost:8080/IPluggableComm";
                    closemsg1.to = "http://localhost:8080/IPluggableComm";
                    RepoComm.postMessage(closemsg1);
                    CommMessage closemsg2 = new CommMessage(CommMessage.MessageType.closeSender);
                    closemsg2.from = "http://localhost:8080/IPluggableComm";
                    closemsg2.to = "http://localhost:8080/IPluggableComm";
                    RepoComm.postMessage(closemsg2);
                    break;
                }
            }
        }
        /*-------return a list contains file names in the given path--------*/

        private List<string> getFileNames(string path)
        {
            List<string> fileNames = new List<string>();
            string[] files = Directory.GetFiles(repoXmlPath);
            foreach (string file in files)
                fileNames.Add(Path.GetFileName(file));
            return fileNames;
        }
        /*-------Check the local Xml storage for the given Xml--------*/

        private void checkXml(CommMessage msg)
        {
            int tryCount = 0;
            while (!getFileNames(repoXmlPath).Contains(msg.xmlName))
            {
                List<string> files = getFileNames(repoXmlPath);
                if (tryCount++ == maxCount)
                {
                    Console.Write("\n  Time out, please try to debug again \n");
                    return;
                }
                Console.Write("\n  tried {0} times \n", tryCount);
                Thread.Sleep(500);
            }
            msg.command = "build";
            msg.to = "http://localhost:8081/IPluggableComm";
            msg.from = "http://localhost:8080/IPluggableComm";
            RepoComm.postMessage(msg);
        }
        /*-------clean all of the files in the given path--------*/

        private void cleanFiles(string cleanPath)
        {
            string[] xmlFiles = Directory.GetFiles(cleanPath);
            foreach (string file in xmlFiles)
                File.Delete(file);
        }

        static void Main(string[] args)
        {
            Console.Title = "Repo";
            ClientEnvironment.verbose = true;
            Repo repoMock = new Repo();
#if (TEST_CHILDPROC)
            Console.Write("\nAutomatically test the function of the Repo");
            Console.Write("\n -automatically build the files from the RepoStorage");
            Process.Start(Path.GetFullPath("..//..//..//builder//bin//Debug//builder.exe"), "3");
            CommMessage msg = new CommMessage(CommMessage.MessageType.request);
            repoMock.checkXml(msg);
#endif
            repoMock.repoLoop();
            Console.Write("\n  Press key to exit");
        }
    }
}