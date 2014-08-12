using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PackageInstallationService;
using PackageInstallationService.Impl;
using PackageInstallationService.Interfaces;

namespace PackageInstallationServiceTest
{
    /// <summary>
    /// Summary description for XmlMetadataProccessor
    /// </summary>
    [TestClass]
    public class XmlMetadataProccessorTest
    {
//        public XmlMetadataProccessorTest()
//        {
//        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        private static string path = "D:\\testdir\\" + Guid.NewGuid() + ".tmp_test";

        private static string pathToInstall = "D:\\testdir\\";
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext)
        {
           
            var xmlMetadata ="<?xml version=\"1.0\" encoding=\"utf-8\" ?>" +
                               "<metadata>" +
                                "<packageName>testp</packageName>" +
                                "<OS>Windows</OS>" +
                                "<Version>1.0.0</Version>" +
                                "<PathToInstall>"+ pathToInstall + "</PathToInstall>" +
                               "</metadata>";
            File.WriteAllText(path,xmlMetadata);
        }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        [ClassCleanup()]
        public static void MyClassCleanup()
        {
            File.Delete(path);
        }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void ProccessTest()
        {
           IMetadataProccessor xmlProccessor = new XmlMetadataProcessor();

            var buf = new PackageInfo.NamedInstance();

            xmlProccessor.Process(path, buf);

            Assert.IsTrue(buf.Metadata.FolderToInstall == pathToInstall);
        }
    }
}
