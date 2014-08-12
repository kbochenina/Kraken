using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PackageInstallationService.Impl;

namespace PackageInstallationServiceTest
{
    /// <summary>
    /// Summary description for SVNStructureFileProcessor
    /// </summary>
    [TestClass]
    public class SvnStructureFileProcessorTest
    {
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

        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext)
        {
            var structureFileData = "FirstPackage/\n" +
                                    "FirstPackage/Unix/\n" +
                                    "FirstPackage/Unix/tags/\n" +
                                    "FirstPackage/Unix/tags/FirstVersion/\n" +
                                    "FirstPackage/Unix/tags/FirstVersion/metadata.xml\n" +
                                    "FirstPackage/Unix/tags/FirstVersion/testp.zip\n" +
                                    "FirstPackage/Unix/tags/SecondVersion/\n" +
                                    "FirstPackage/Unix/tags/SecondVersion/metadata.xml\n" +
                                    "FirstPackage/Unix/tags/SecondVersion/testp.zip\n" +
                                    "FirstPackage/Unix/trunk/\n" +
                                    "FirstPackage/Unix/trunk/metadata.xml\n" +
                                    "FirstPackage/Unix/trunk/testp.zip\n" +
                                    "FirstPackage/Windows/\n" +
                                    "FirstPackage/Windows/tags/\n" +
                                    "FirstPackage/Windows/tags/ProtoVersion/\n" +
                                    "FirstPackage/Windows/tags/ProtoVersion/metadata.xml\n" +
                                    "FirstPackage/Windows/tags/ProtoVersion/testp.zip\n" +
                                    "FirstPackage/Windows/tags/SecondVersion/\n" +
                                    "FirstPackage/Windows/tags/SecondVersion/metadata.xml\n" +
                                    "FirstPackage/Windows/tags/SecondVersion/testp.zip\n" +
                                    "FirstPackage/Windows/tags/ThirdVersion/\n" +
                                    "FirstPackage/Windows/tags/ThirdVersion/metadata.xml\n" +
                                    "FirstPackage/Windows/tags/ThirdVersion/testp.zip\n" +
                                    "FirstPackage/Windows/trunk/\n" +
                                    "FirstPackage/Windows/trunk/metadata.xml\n" +
                                    "FirstPackage/Windows/trunk/testp.zip\n"; 
            File.WriteAllText(path, structureFileData);
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
        public void ProcessTest()
        {
            var processor = new SVNStructureFileProcessor("metadata.xml");

            var list = processor.Process(path);

            Assert.IsTrue(list.Count == 1);

            Assert.IsTrue(list.ElementAt(0).VersionedInstances.ContainsKey("Unix"));

            Assert.IsTrue(list.ElementAt(0).VersionedInstances.ContainsKey("Windows"));

            var unix = list.ElementAt(0).VersionedInstances["Unix"];
            var windows = list.ElementAt(0).VersionedInstances["Windows"];

            Assert.IsTrue(unix.Count == 3);

            Assert.IsTrue(windows.Count == 4);
        }
    }
}
