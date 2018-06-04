/////////////////////////////////////////////////////////////////////////////
//  Builder.cs - generate and start the child process, pass the requests   //
//               to the child process                                      //
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
 *   This package implements the builder which generates and starts child
 *   process, and passes requests to the child process, you could change 
 *   the number of child process by changing the command line arguments.
 * 
 * 
 *   Public Interface
 *   ----------------
 *   class Builder:
 *   public void commTest()                          -simple test for the function of BldComm(the communicative component)
 *   public void BuilderLoopTest()                   -simple test for the BuilderLoop() function
 *   public void BuilderLoop()                       -continue get and process message
 *   public void getReadyThread(CommMessage msg)     -extract the prot number from the message got
 *   
 *   class SpawnProc:
 *   static bool createProcess(int i)                -generate and start child process, the number is given by the command line arguments.
 *   
 * 
 */
/*
 *   Build Process
 *   -------------
 *   - Required files:   IMessagePassingCommService.cs MessagePassingCommService.cs builder.cs
 * 
 *   Maintenance History
 *   -------------------
 *   ver 1.0 : 27 October 2017
 *     - first release
 *   ver 2.0 : 6 December 2017
 * 
 */

using MessagePassingComm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Builder
{
    class Builder
    {
        public static SWTools.BlockingQueue<string> trdQ { get; set; } = null;                          //queue for ready thread(port number only)
        public static SWTools.BlockingQueue<CommMessage> reqQ { get; set; } = null;                     //queue for request
        public int localHost { get; set; } = 0;                                                         //local host port number
        public List<int> portList { get; set; } = null;                                                 //child process port number list
        public int portNum { get; set; } = 0;                                                           //portNum = localHost + i, child process port numer
        private Comm BldComm;                                                                           //communicative part of builder
        private const string chdXmlPath = "../../../ChildProc/Xml";

        public Builder(int hostNum)
        {
            trdQ = new SWTools.BlockingQueue<string>();
            reqQ = new SWTools.BlockingQueue<CommMessage>();
            localHost = hostNum;
            portList = new List<int>();
            BldComm = new Comm("http://localhost", localHost);
            cleanFiles(chdXmlPath);
        }
        /*----------------test communactive component---------------------------*/

        public void commTest()
        {
            CommMessage csndMsg1 = new CommMessage(CommMessage.MessageType.request);
            csndMsg1.command = "this is test message to 8082";
            csndMsg1.author = "Yuan Liu";
            csndMsg1.to = "http://localhost:8082/IPluggableComm";
            csndMsg1.from = "http://localhost:8081/IPluggableComm";

            CommMessage csndMsg2 = new CommMessage(CommMessage.MessageType.request);
            csndMsg2.command = "this is a test message to 8083";
            csndMsg2.author = "Yuan Liu";
            csndMsg2.to = "http://localhost:8083/IPluggableComm";
            csndMsg2.from = "http://localhost:8081/IPluggableComm";

            CommMessage csndMsg3 = new CommMessage(CommMessage.MessageType.request);
            csndMsg3.command = "this is a test message to 8084";
            csndMsg3.author = "Yuan Liu";
            csndMsg3.to = "http://localhost:8084/IPluggableComm";
            csndMsg3.from = "http://localhost:8081/IPluggableComm";

            BldComm.postMessage(csndMsg1);
            csndMsg1.show();
            Thread.Sleep(1000);
            BldComm.postMessage(csndMsg2);
            csndMsg2.show();
            Thread.Sleep(1000);
            BldComm.postMessage(csndMsg3);
            csndMsg3.show();

        }
        /*-------------------------test builderloop-----------------------------*/

        public void BuilderLoopTest()
        {
            for (int i = 0; i < 10; i++)
            {
                CommMessage msgTest = new CommMessage(CommMessage.MessageType.request);
                msgTest.command = i.ToString();
                msgTest.author = "Yuan Liu";
                msgTest.to = "http://localhost:8081/IPluggableComm";
                msgTest.from = "http://localhost:8081/IPluggableComm";
                BldComm.postMessage(msgTest);
            }
            BuilderLoop();
        }
        /*----builder loop: dispach the build request to the child process------*/

        public void BuilderLoop()
        {
            while(true)
            {
                CommMessage msg = BldComm.getMessage();
                msg.show();
                if (msg.command == "ready")
                {
                    getReadyThread(msg);
                }
                else if (msg.type != CommMessage.MessageType.connect && msg.type != CommMessage.MessageType.close)
                {
                    reqQ.enQ(msg);
                }
                if(msg.type == CommMessage.MessageType.close)
                {
                    foreach(int portnum in portList)
                    {
                        CommMessage closemsg = new CommMessage(CommMessage.MessageType.close);
                        closemsg.from = "http://localhost:8081/IPluggableComm";
                        closemsg.to = "http://localhost:" + portnum + "/IPluggableComm";
                        BldComm.postMessage(closemsg);
                    }
                    postCloseTester();
                    CommMessage closemsg1 = new CommMessage(CommMessage.MessageType.closeReceiver);
                    closemsg1.from = "http://localhost:8081/IPluggableComm";
                    closemsg1.to = "http://localhost:8081/IPluggableComm";
                    BldComm.postMessage(closemsg1);
                    CommMessage closemsg2 = new CommMessage(CommMessage.MessageType.closeSender);
                    closemsg2.from = "http://localhost:8081/IPluggableComm";
                    closemsg2.to = "http://localhost:8081/IPluggableComm";
                    BldComm.postMessage(closemsg2);
                    break;
                }
                if (trdQ.size() != 0 && reqQ.size() != 0)
                {
                    string portTo = trdQ.deQ();
                    CommMessage buildRequest = reqQ.deQ();
                    buildRequest.to = "http://localhost:" + portTo + "/IPluggableComm";
                    BldComm.postMessage(buildRequest);
                }
            }
        }
        /*----generate and send the close message to the tester------*/

        private void postCloseTester()
        {
            CommMessage msg = new CommMessage(CommMessage.MessageType.request);
            msg.from = "http://localhost:8081/IPluggableComm";
            msg.to = "http://localhost:8078/IPluggableComm";
            msg.command = "close";
            BldComm.postMessage(msg);
        }
        /*---------------clean all of the files in the given path---------------*/

        private void cleanFiles(string cleanPath)
        {
            string[] xmlFiles = Directory.GetFiles(cleanPath);
            foreach (string file in xmlFiles)
                File.Delete(file);
        }

        /*-------------extract port number from the ready message from the  child process--------------*/

        public void getReadyThread(CommMessage msg)
        {
            trdQ.enQ(msg.from.Substring(17,4));                                         //get the sub string of the port number
        }
    }
    class SpawnProc
    {
        /*--------------generate and start child process-----------------------*/
        static bool createProcess(int i)
        {
            Process proc = new Process();
            string fileName = "..\\..\\..\\ChildProc\\bin\\debug\\ChildProc.exe";         //backslash may not work
            string absFileSpec = Path.GetFullPath(fileName);

            Console.Write("\n  attempting to start {0}", absFileSpec);
            string commandline = i.ToString();
            try
            {
                Process.Start(fileName, commandline);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
                return false;
            }
            return true;
        }
        static void Main(string[] args)
        {
            Console.Title = "Builder";

            Console.Write("\n  Demo Parent Process");
            Console.Write("\n =====================");

            if (args.Count() == 0)
            {
                Console.Write("\n  please enter number of processes to create on command line");
                return;
            }
            else
            {
                int count = Int32.Parse(args[0]);
                Builder builder = new Builder(8081);
                for (int i = 1; i <= count; ++i)
                {
                    builder.portNum = builder.localHost + i;
                    if (createProcess(builder.portNum))
                    {
                        Console.Write(" - succeeded");
                        builder.portList.Add(builder.portNum);
                    }
                    else
                    {
                        Console.Write(" - failed");
                    }
                }
#if (TEST_BUILDER)
                Console.Write("\n  ---------------------------");
                Console.Write("\n  Test the function of the builder by sending each child a message\n");
                builder.commTest();
                Console.Write("\n  ---------------------------");
                Console.Write("\n  Test the function of the builder by sending 10 messages to itself\n");
                builder.BuilderLoopTest(); 
#endif
                builder.BuilderLoop();
            }
            Console.Write("\n  Press key to exit");
        }
    }
}
