using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ControllerFarmService.Installation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ControllerFarmServiceTest
{
    [TestClass]
    public class PackageObtainerImplTest
    {
        private static string userName;
        private static string password;
        private static int clearInterval;
        private static int lifeInterval;
        private static string tempDir;

        [ClassInitialize]
        public static void TestInitialize()
        {
            userName = "admin";
            password = "admin";
            clearInterval = 1000;
            lifeInterval = 3*1000;
            tempDir = "\\obtainFolder";

            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
        }

        [TestMethod]
        public void ObtainPackageTest()
        {
//            string address = "";
//
//            var obtainer = new PackageObtainerImpl(userName,password,clearInterval,lifeInterval,tempDir);
//            var downloadedEvent = new CountdownEvent(4);
//            Action<string, Exception> callback = (s, ex) =>
//            {
//                downloadedEvent.AddCount();
//            };
//            
//            obtainer.ObtainPackage(address,callback);
//
//            obtainer.ObtainPackage(address,callback);
//
//            obtainer.ObtainPackage(address, callback);
//            
//            downloadedEvent.Wait();
//
//            obtainer.ObtainPackage();
        }
    }
}
