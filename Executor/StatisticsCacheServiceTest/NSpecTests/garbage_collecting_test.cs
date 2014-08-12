using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CommonDataTypes.RExService.Service.Entity.Info;
using NSpec;
using StatisticsCacheService.Cache;
using StatisticsCacheService.Cache.FileDumping;
using StatisticsCacheService.Cache.GarbageCollecting;

namespace StatisticsCacheServiceTest.NSpecTests
{
    class garbage_collecting_test:nspec
    {
        private const string TEMP_DIRECTORY = "D:\\testdir\\StatisticsCacheServiceTesting";

        private const int LIFE_TIME = 4*1000;
        private const int GC_INTERVAL = 5*1000;

        private const int FIRST_DELAY = 3*1000;
        private const int SECOND_DELAY = 3*1000;

        private static readonly DateTime FROM_THE_VERY_BEGGING = new DateTime(1970,1,1); 


        void before_all()
        {
           Helper.InitTempDir(TEMP_DIRECTORY);
           Console.WriteLine("before_all");
        }

        void trashing_and_dumping()
        {
            //Dictionary<string, Dictionary<string, List<NodeInfo>>> data = null;
            ResourceCache cache = null;

            int dataCount = -1;
            int newDataCount = -1;

            context["Create initial mock"] = () =>
            {
                before = () =>
                {
                    var data = Helper.createMockData("resource_a","node_a");
                    dataCount = Helper.GetCount(data);
                    cache = Helper.CreateResourceCache(TEMP_DIRECTORY, LIFE_TIME, GC_INTERVAL);
                    cache.AddAllInfo(data);
                    Console.WriteLine("upper_before + " + dataCount);
                };

                it["should be equal mock's count"] = () =>
                {
                    (Helper.GetCount(cache.GetAllInfoStartedWith(FROM_THE_VERY_BEGGING)) == dataCount) 
                            .should_be_true();
                };

                context["empty context"] = () =>
                    {
                        Console.WriteLine("Hello World!");
                    };
            }; 

            context["try to make first delay and check cache after it"] = () =>
            {
                

                before = () =>
                {
                    Thread.Sleep(FIRST_DELAY);
                    var new_data = Helper.createMockData("resource_b","node_b");
                    newDataCount = Helper.GetCount(new_data);
                    Console.WriteLine("first_context_before + " + newDataCount);
                    cache.AddAllInfo(new_data);
                    
                };

                it["should have data + new_data = all_data_count"] =
                    () =>
                    {
                        (Helper.GetCount(cache.GetAllInfoStartedWith(FROM_THE_VERY_BEGGING)) == dataCount + newDataCount)
                            .should_be_true();
                    };
            };

            Console.WriteLine("=====><===");

            context["check cache after the second delay"] = () =>
            {
                before = () => { Thread.Sleep(SECOND_DELAY); Console.WriteLine("asssd"); };

                it["should contain only new_data"] = () =>
                {
                    (Helper.GetCount(cache.GetAllInfoStartedWith(FROM_THE_VERY_BEGGING)) == newDataCount)
                        .should_be_true();
                };
            };
            
            context["data should have been saved in tempDirectory"] = () =>
            {
                string[] files = null;
                before = () => { files = CheckDirectory(TEMP_DIRECTORY); Console.WriteLine("Hello, tempDir"); };

                it["should contain 1 saved file"] = () => { (files.Count() == 1).should_be_true(); };

                //it["with name resource ---"] = () =>{/*check name*/};
            };
        }

        string[] CheckDirectory(string path)
        {
           var files = Directory.GetFiles(path, "resource*");
            return files;
        }
    }

    static class Helper
    {

        static NodeInfo createNodeInfo(DateTime? dt = null)
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

        static List<NodeInfo> createNodeInfos(Tuple<int/*min*/, int/*max*/> countInterval,
                                                Tuple<DateTime/*older*/, DateTime/*newer*/> timeInterval)
        {
            var DEFAULT_TIME = DateTime.Now;

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

        static Dictionary<string, List<NodeInfo>> createResource(int nodeCount, string baseName, Tuple<int/*min*/, int/*max*/> countInterval, Tuple<DateTime/*older*/, DateTime/*newer*/> timeInterval)
        {
            Dictionary<string, List<NodeInfo>> nodes = new Dictionary<string, List<NodeInfo>>();

            for (int i = 0; i < nodeCount; ++i)
            {
                nodes.Add(baseName + "_" + i, createNodeInfos(countInterval, timeInterval));
            }

            return nodes;
        }

        static Dictionary<string, Dictionary<string, List<NodeInfo>>> createFarm(int resCount, string resBaseName, string nodeBaseName,
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

        public static Dictionary<string, Dictionary<string, List<NodeInfo>>> createMockData(string resourceName, string nodeName)
        {
            return createFarm(1, resourceName, nodeName, new Tuple<int, int>(5, 5),
                new Tuple<int, int>(2, 2),
                new Tuple<DateTime, DateTime>(new DateTime(2000, 1, 1, 1, 1, 1, 1), new DateTime(2013, 1, 1, 1, 1, 1, 1)));
        }

        public static int GetCount(Dictionary<string, Dictionary<string, List<NodeInfo>>> data)
        {
           return data.Aggregate(0, (seed,x) => seed + x.Value.Aggregate(0, (seed1, y) => seed1 + x.Value.Count));
        }

        public static ResourceCache CreateResourceCache(string path, int lifeTime, int gcInterval)
        {
            var result = new ResourceCache(new TimeSpan(0,0,0,0,lifeTime));
            var dumper = new DataDumper(path, "resource");
            var gc = new TreeCacheGarbageCollector(result,gcInterval,dumper);
            return result;
        }

        public static void InitTempDir(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            Directory.CreateDirectory(path);
        }
    }
}
