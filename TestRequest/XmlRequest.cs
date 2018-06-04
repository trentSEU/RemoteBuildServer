/////////////////////////////////////////////////////////////////////
// XmlRequest.cs - build and parse Xml requests                    //
//                                                                 //
// Author: Yuan Liu, yliu219@syr.edu                               //
// Refer to: Jim Fawcett                                           //
// Application: CSE681-Software Modeling and Analysis Assignment   //
// Environment: C# console                                         //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * ===================
 * Creates and parses TestRequest XML messages using XDocument
 * 
 * Public Interface
 * ----------------
 * public class XmlRequest:
 * public void makeRequest()                                        -build XML document that represents a test request
 * public bool loadXml(string path)                                 -load TestRequest from XML file
 * public bool saveXml(string path)                                 -save TestRequest to XML file
 * public string parse(string propertyName)                         -parse document for property value
 * public List<string> parseList(string propertyName)               -parse document for property list
 * 
 * 
 * Required Files:
 * ---------------
 * XmlRequest.cs
 * 
 * Maintenance History:
 * --------------------
 * ver 1.0 : 05 Oct 2017
 * - first release
 * ver 2.0 : 6 December 2017
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace XmlMessage
{
    ///////////////////////////////////////////////////////////////////
    // TestRequest class

    public class XmlRequest
    {
        public string dir { get; set; } = "";
        public string size { get; set; } = "";
        public List<string> files { get; set; } = new List<string>();
        public XDocument doc { get; set; } = new XDocument();

        /*----< build XML document that represents a test request >----*/

        public void makeRequest()
        {
            XElement RequestElem = new XElement("Request");
            doc.Add(RequestElem);

            XElement sizeElem = new XElement("size");
            sizeElem.Add(size);
            RequestElem.Add(sizeElem);

            XElement dirElem = new XElement("dir");
            dirElem.Add(dir);
            RequestElem.Add(dirElem);

            foreach (string file in files)
            {
                XElement fileElem = new XElement("file");
                fileElem.Add(file);
                RequestElem.Add(fileElem);
            }
        }
        /*----< load TestRequest from XML file >-----------------------*/

        public bool loadXml(string path)
        {
            try
            {
                doc = XDocument.Load(path);
                return true;
            }
            catch (Exception ex)
            {
                Console.Write("\n--{0}--\n", ex.Message);
                return false;
            }
        }
        /*----< save TestRequest to XML file >-------------------------*/

        public bool saveXml(string path)
        {
            try
            {
                doc.Save(path);
                return true;
            }
            catch (Exception ex)
            {
                Console.Write("\n--{0}--\n", ex.Message);
                return false;
            }
        }
        /*----< parse document for property value >--------------------*/

        public string parse(string propertyName)
        {
            //System.InvalidOperationException: 'Sequence contains no elements'
            string parseStr = doc.Descendants(propertyName).First().Value;
            //System.InvalidOperationException: 'Sequence contains no elements'
            if (parseStr.Length > 0)
            {
                switch (propertyName)
                {
                    case "dir":
                        dir = parseStr;
                        break;
                    case "size":
                        size = parseStr;
                        break;
                    default:
                        break;
                }
                return parseStr;
            }
            return "";
        }
        /*----< parse document for property list >---------------------*/
        /*
        * - now, there is only one property list for tested files
        */
        public List<string> parseList(string propertyName)
        {
            List<string> values = new List<string>();

            IEnumerable<XElement> parseElems = doc.Descendants(propertyName);

            if (parseElems.Count() > 0)
            {
                switch (propertyName)
                {
                    case "file":
                        foreach (XElement elem in parseElems)
                        {
                            values.Add(elem.Value);
                        }
                        files = values;
                        break;
                    default:
                        break;
                }
            }
            return values;
        }
    }
#if (TEST_XMLREQUEST)

    ///////////////////////////////////////////////////////////////////
    // TestRepoMock class

    class TestRepoMock
    {
        static void Main(string[] args)
        {
            Console.Write("make a xml message, and save it to TestHarness\\TestStorage\\TestXml.xml \n");
            Console.Write("============================================\n");
            string fileSpec = @"../../../TestHarness/TestStorage/TestXml.xml";
            XmlRequest req = new XmlRequest();
            req.testedFile = "Tested";
            req.testDriver.Add("SimpleAdd.Test1");
            req.testDriver.Add("SimpleAdd.Test2");
            req.testDriver.Add("SimpleAdd.Test3");
            req.makeRequest();
            req.saveXml(fileSpec);

            Console.Write("load and parse the xml message constructed\n");
            XmlRequest reqGet = new XmlRequest();
            reqGet.loadXml(fileSpec);
            reqGet.parse("testedFile");
            Console.Write("\n testedFile: {0}\n", reqGet.testedFile);
            reqGet.parseList("testDriver");
            foreach (string t in reqGet.testDriver)
                Console.Write(" testDriver: {0}\n", t);
            Console.Write("\n\n");
        }
    }
#endif
}

