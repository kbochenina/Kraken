using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ControllerFarmService.Installation;
using ControllerFarmService.Installation.Impl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ControllerFarmServiceTest
{
    /// <summary>
    /// Summary description for PackageCacherImplTest
    /// </summary>
    [TestClass]
    public class PackageCacherImplSynchronousTest
    {
        static void Main(string[] args)
        {
            var test = new PackageCacherImplSynchronousTest();

            test.CommonTest();
        }

        public PackageCacherImplSynchronousTest()
        {
            //
            // TODO: Add constructor logic here
            //
        }

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
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext)
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir);
            }
            Console.WriteLine(Directory.CreateDirectory(tempDir).FullName);
        }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
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

        private static int clearInterval = 2000;
        private static int lifeTime = 4000;
        private static string tempDir = "testTempDir";

        [TestMethod]
        public void CommonTest()
        {
            //Scenario:
            //create cacher
            
            //create two files, they will be out-of-time
            //create third file, it will be out-of-time too, but cacher mustn't delete it as it will be locked by reading
            //create forth file, it will be out-of-time too, but cacher mustn't delete it as it will touched.
 
            //make a delay according to time-of-life 

            //create fifth, it won't be expired
            
            //open third file on read
            //touch forth to overcome out-of-time

            //take a small delay

            //conclude what exists now

            //run cleaning by cacher

            //see what are lasted

            //there must be third, forth and fifth

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir);
            }
            Console.WriteLine(Directory.CreateDirectory(tempDir).FullName);
            
            
            var FIRST_KEY = "first.txt";
            var SECOND_KEY = "second.txt";
            var THIRD_KEY = "third.txt";
            var FORTH_KEY = "forth.txt";
            var FIFTH_KEY = "fifth.txt";
           

            var cacher = new PackageCacherImpl(clearInterval, lifeTime, tempDir);

           var FIRST = cacher.ReserveAddress(FIRST_KEY);
           var SECOND = cacher.ReserveAddress(SECOND_KEY);
           var THIRD = cacher.ReserveAddress(THIRD_KEY);
           var FORTH = cacher.ReserveAddress(FORTH_KEY);
           var FIFTH = cacher.ReserveAddress(FIFTH_KEY);


            CreateFiles(FIRST,SECOND,THIRD,FORTH);
            RegisterFiles(cacher, FIRST_KEY, SECOND_KEY, THIRD_KEY, FORTH_KEY);

            Thread.Sleep(5*1000);

            CreateFile(FIFTH);
            RegisterFiles(cacher, FIFTH_KEY);

            var readingFile = ReadFile(THIRD);
            
            //It's synchronius
            cacher.Get(FORTH_KEY,(x)=>{});

            Thread.Sleep(2 * 1000);

            Assert.IsTrue(CheckExisting(FIRST,SECOND,THIRD,FORTH,FIFTH));

            cacher.Clean();

            Assert.IsTrue(CheckExisting(THIRD,FORTH,FIFTH));
        }

        [TestMethod]
        public void SubscribersTest()
        {
            //Scenario:
            //reserve package name
            //try to get package by first subscriber
            //try to get package by second subscriber
            //register downloaded package

            //the first subscriber should get notification
            //the second subscriber should get notification

            //the third subscriber appears and get package local address
            var reservationKey = "ReservationKey";

            var cacher = new PackageCacherImpl(clearInterval, lifeTime, tempDir);

            var address = cacher.ReserveAddress(reservationKey);

            var firstSubscriberGotPackage = false;
            cacher.Get(reservationKey, (response) => { firstSubscriberGotPackage = true; });

            var secondSubscriberGotPackage = false;
            cacher.Get(reservationKey, (response) => { secondSubscriberGotPackage = true; });

            cacher.RegisterDownloaded(reservationKey, 0);

            var thirdSubscriberGotPackage = false;
            cacher.Get(reservationKey, (response) => { thirdSubscriberGotPackage = true; });

            Assert.IsTrue(firstSubscriberGotPackage);

            Assert.IsTrue(secondSubscriberGotPackage);

            Assert.IsTrue(thirdSubscriberGotPackage);

        }

        private void CreateFile(string name, string text="")
        {
            using (var file = File.CreateText(/*Path.Combine(tempDir,*/ name/*)*/))
            {
                file.WriteLine(text);
            }
        }

        private void TouchFile(string name)
        {
            File.ReadAllText(name);

            //File.AppendAllText(name, "text");
//            using (var file = File.AppendAllLines( /*Path.Combine(tempDir,*/name /*)*/))
//            {
//                file.
//            }
        }

        private FileStream ReadFile(string name)
        {
            var file = File.OpenRead(/*Path.Combine(tempDir,*/name/*)*/);
            return file;
        }

        private bool CheckExisting(params string[] shouldExist)
        {
            
            var files = Directory.GetFiles(tempDir);
            Log(files);
            Log(shouldExist);
            return files.Count() == shouldExist.Length && !files.Except(shouldExist).Any();
        }

        private void Log(IEnumerable<string> data)
        {
            int i = 0;
            foreach (var el in data)
            {
                Console.WriteLine(i + "): " + el);
                ++i;
            }
        }

        private void CreateFiles(params string[] names)
        {
            foreach (var name in names)
            {
                CreateFile(name);
            }
        }

        private void RegisterFiles(PackageCacherImpl cacher, params string[] keys)
        {
            foreach (var key in keys)
            {
                cacher.RegisterDownloaded(key,0);
            }
        }
    }
}
