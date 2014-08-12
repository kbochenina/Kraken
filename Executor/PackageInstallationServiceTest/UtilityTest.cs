using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PackageInstallationService;

namespace PackageInstallationServiceTest
{
    [TestClass]
    public class UtilityTest
    {
        [TestMethod]
        public void DownloadFileByPathTest()
        {
            //svn must be up
            var path = "http://fonhorst-c2q:18080/svn/StructureInfoRepo/StructureList.txt";
            var localPath = "D:/testdir/" + Guid.NewGuid().ToString() + ".tmp";
            var username = "admin";
            var password = "admin";

            Assert.IsTrue(!File.Exists(localPath));

            InstallationUtility.DownloadFileByPath(path, localPath, username, password);

            Assert.IsTrue(File.Exists(localPath));
        }


    }
}
