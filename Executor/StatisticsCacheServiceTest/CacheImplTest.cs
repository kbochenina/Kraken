using System.Linq;
using System.Reflection;
using System.Threading;
using CommonDataTypes.RExService.Service.Entity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Runtime.Caching;
using StatisticsCacheService;
using CommonDataTypes.RExService.Service.Entity.Info;
using StatisticsCacheService.Cache;

namespace StatisticsCacheServiceTest
{
    
    
    /// <summary>
    ///This is a test class for CacheImplTest and is intended
    ///to contain all CacheImplTest Unit Tests
    ///</summary>
    [TestClass()]
    public class CacheImplTest
    {
        private static DateTime DEFAULT_TIME = new DateTime(2000, 1, 1, 1, 1, 1, 1);

        public static void Main(string[] args)
        {
            CacheImplTest cit = new CacheImplTest();

            cit.Initialize();

            cit.HighLoadTesting();
            //cit.AddAllResourcesInfoTest();

        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        private Dictionary<string, Dictionary<string, List<NodeInfo>>> mockData;

        private NodeInfo createNodeInfo(DateTime? dt = null)
        {
            NodeInfo node = new NodeInfo();

            node.DiskAvailableFreeSpace = 100500;
            node.DiskUsage = 43.5;
            node.MemAvailable = 54.3;
            node.MemUsage = 34.2;
            node.Net = 3.14;
            node.Offline = false;
            node.ProcUsage = 76.8;
            node.SandBoxTotalSize = 455000;
            node.TimeSnapshot = dt ?? DateTime.Now;

            return node;
        }

        private List<NodeInfo> createNodeInfos(Tuple<int/*min*/, int/*max*/> countInterval,
                                                Tuple<DateTime/*older*/, DateTime/*newer*/> timeInterval)
        {
            var rand = new Random();
            int count = rand.Next(countInterval.Item1, countInterval.Item2);

            List<NodeInfo> data = new List<NodeInfo>();
            for (int i = 0; i < count; ++i)
            {
                //todo repair generator later
                //long dt = rand.Next((int)timeInterval.Item1.Ticks >> 32, (int)timeInterval.Item2.Ticks >> 32);
                //var lowBase = (timeInterval.Item1.Ticks << 32) >> 32;
                data.Add(createNodeInfo(DEFAULT_TIME));
            }
            return data;
        }

        private Dictionary<string, List<NodeInfo>> createResource(int nodeCount, string baseName, Tuple<int/*min*/, int/*max*/> countInterval, Tuple<DateTime/*older*/, DateTime/*newer*/> timeInterval)
        {
            Dictionary<string, List<NodeInfo>> nodes = new Dictionary<string, List<NodeInfo>>();

            for (int i = 0; i < nodeCount; ++i)
            {
                nodes.Add(baseName + "_" + i, createNodeInfos(countInterval, timeInterval));
            }

            return nodes;
        }

        private Dictionary<string, Dictionary<string, List<NodeInfo>>> createFarm(int resCount, string resBaseName, string nodeBaseName,
                                                                                    Tuple<int, int> nodeCountInterval,
                                                                                    Tuple<int, int> countInterval,
                                                                                    Tuple<DateTime/*older*/, DateTime/*newer*/> timeInterval)
        {

            Dictionary<string, Dictionary<string, List<NodeInfo>>> data = new Dictionary<string, Dictionary<string, List<NodeInfo>>>();

            Random rd = new Random();
            for (int i = 0; i < resCount; ++i)
            {
                data.Add(resBaseName + "_" + i, createResource(rd.Next(nodeCountInterval.Item1, nodeCountInterval.Item2),
                                                                nodeBaseName, countInterval, timeInterval));
            }
            return data;
        }

        private Dictionary<string, Dictionary<string, List<NodeInfo>>> createHighLoadMockData()
        {
            return createFarm(100, "resource", "node", new Tuple<int, int>(50, 100),
                new Tuple<int, int>(1000, 2000),
                new Tuple<DateTime, DateTime>(new DateTime(2000, 1, 1, 1, 1, 1, 1), new DateTime(2013, 1, 1, 1, 1, 1, 1)));
        }

        [TestInitialize()]
        public void Initialize()
        {
            mockData = createHighLoadMockData();
        }

        // [TestMethod()]
        public void ExpirationTest()
        {
            //            CacheImpl target = new CacheImpl(new TimeSpan(0,0,2));
            //            target.AddAllInfo(DateTime.Now,mockInsertData);
            //            Assert.IsTrue(target.Count() == 1);
            //            Thread.Sleep(5000);
            //            Assert.IsTrue(target.Count() == 0);

        }

        /// <summary>
        ///A test for AddAllInfo
        ///</summary>
        // [TestMethod()]
        public void AddAllResourcesInfoTest()
        {
            //            CacheImpl target = new CacheImpl();
            //
            //            var startedWith = new DateTime(2013, 08, 16, 17, 46, 10);
            //
            //            
            //
            //            var firstTime = new DateTime?(new DateTime(2013, 08, 16, 17, 46, 20));
            //            var secondTime = new DateTime?(new DateTime(2013, 08, 16, 17, 46, 30));
            //            var thirdTime = new DateTime?(new DateTime(2013, 08, 16, 17, 46, 40));
            //            var forthTime = new DateTime?(new DateTime(2013, 08, 16, 17, 46, 0));
            //
            //            var firstStamp = createMock(firstTime);
            //            var secondStamp = createMock(secondTime);
            //            var thirdStamp = createMock(thirdTime);
            //            var forthStamp = createMock(forthTime);
            //
            //            target.AddAllInfo(forthTime.Value, forthStamp);
            //            target.AddAllInfo(secondTime.Value, secondStamp);
            //            target.AddAllInfo(thirdTime.Value, thirdStamp);
            //            target.AddAllInfo(firstTime.Value, firstStamp);
            //
            //            var result = target.GetAllInfoStartedWith(startedWith);
            //
            //            Assert.IsTrue(result.Count == 3);
            //
            //            var stack = typeof(CacheImpl).GetField("_orderedSequence", BindingFlags.NonPublic | BindingFlags.Instance).
            //                                            GetValue(target) as SortedList<DateTime, CacheItem>;
            //
            //            Assert.IsTrue(stack.Count == 4);
            //            Assert.IsTrue(stack.ElementAt(0).Key == thirdTime);
            //            Assert.IsTrue(stack.ElementAt(1).Key == secondTime);
            //            Assert.IsTrue(stack.ElementAt(2).Key == firstTime);
            //            Assert.IsTrue(stack.ElementAt(3).Key == forthTime);
        }
        


        [TestMethod()]
        public void HighLoadTesting()
        {
            int k = 0;
            ResourceCache target = new ResourceCache();

            target.AddAllInfo(mockData);
        }

    }
}
