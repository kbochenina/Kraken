using System;
using System.Collections.Generic;
using System.IO;
using CommonDataTypes.RExService.Service.Entity.Info;
using StatisticsCacheService.Cache;

namespace StatProfiling
{
    public class ProfilingSamples
    {
        private static readonly DateTime DEFAULT_TIME = new DateTime(2000, 1, 1, 1, 1, 1, 1);

        private Dictionary<string, Dictionary<string, List<NodeInfo>>> mockData;

        private Dictionary<string, Dictionary<string, List<NodeInfo>>> minMockData;

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
                data.Add(createNodeInfo(DEFAULT_TIME.AddMilliseconds(i)));
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

        public Dictionary<string, Dictionary<string, List<NodeInfo>>> createHighLoadMockData()
        {
            return createFarm(100, "resource", "node", new Tuple<int, int>(50, 100)/*50-100 and for using get.. and add.. simultaneously we get OutOfMemoryException*/,
                new Tuple<int, int>(1000, 2000),
                new Tuple<DateTime, DateTime>(new DateTime(2000, 1, 1, 1, 1, 1, 1), new DateTime(2013, 1, 1, 1, 1, 1, 1)));
        }

        public Dictionary<string, Dictionary<string, List<NodeInfo>>> createMiddleSizeMockData()
        {
            return createFarm(100, "resource", "node", new Tuple<int, int>(100, 100)/*50-100 and for using get.. and add.. simultaneously we get OutOfMemoryException*/,
               new Tuple<int, int>(20, 20),
               new Tuple<DateTime, DateTime>(new DateTime(2000, 1, 1, 1, 1, 1, 1), new DateTime(2013, 1, 1, 1, 1, 1, 1)));
        }

        public Dictionary<string, Dictionary<string, List<NodeInfo>>> createMiniMockData()
        {
            return createFarm(10, "resource", "node", new Tuple<int, int>(5, 10),
                new Tuple<int, int>(10, 20),
                new Tuple<DateTime, DateTime>(new DateTime(2000, 1, 1, 1, 1, 1, 1), new DateTime(2013, 1, 1, 1, 1, 1, 1)));
        }

        public void Initialize()
        {
            mockData = createHighLoadMockData();

            minMockData = createMiniMockData();
        }


        public void HighLoadTesting()
        {

            ResourceCache target = new ResourceCache();


            //WriteToFile("mockData.txt",mockData);

            target.AddAllInfo(mockData);

            var result = target.GetAllInfoStartedWith(new DateTime(1999, 1, 1, 1, 1, 1, 1));

            //WriteToFile("resultData.txt", result);
        }

        private void WriteToFile(string filename, Dictionary<string, Dictionary<string, List<NodeInfo>>> data)
        {
            var mockDataFile = File.CreateText(filename);
            mockDataFile.WriteLine("Resource count: " + data.Count);
            foreach (var nodes in data)
            {
                mockDataFile.WriteLine("Resource's nodes count: " + nodes.Value.Count);
                foreach (var node in nodes.Value)
                {
                    mockDataFile.Write(" Node's info count: " + node.Value.Count);
                }
                mockDataFile.WriteLine();
            }
        }

        public void MinMockTesting()
        {

            ResourceCache target = new ResourceCache();

            WriteToFile("mockData.txt", minMockData);

            target.AddAllInfo(minMockData);            

            var result = target.GetAllInfoStartedWith(new DateTime(1999, 1, 1, 1, 1, 1, 1));

            WriteToFile("resultData.txt", result);

        }


    }
}
