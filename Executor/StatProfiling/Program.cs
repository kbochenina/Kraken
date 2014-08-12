using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonDataTypes;
using CommonDataTypes.RExService.Service.Entity.Info;

namespace StatProfiling
{
    class Program
    {
        static void Main(string[] args)
        {
            CorrectnessTesting();

            Console.WriteLine("Please, Press any key...");
            Console.ReadLine();
        }

        private static void LoadTesting()
        {
            var samples = new ProfilingSamples();

            samples.Initialize();

            //            samples.MinMockTesting();
            samples.HighLoadTesting();
        }

        private static void CorrectnessTesting()
        {
            var test1 = CheckDataCorrectness(new Dictionary<string, Dictionary<string, List<NodeInfo>>>());
            Console.WriteLine("Test 1: " + test1);
            var test2 = CheckDataCorrectness("non-empty");
            Console.WriteLine("Test 2: " + test2);
        }

        private static bool CheckDataCorrectness(Dictionary<string, Dictionary<string, List<NodeInfo>>> data)
        {
            return data.All(x => x.Key != null && x.Value != null && x.Value.All(y => y.Key != null && y.Value != null && y.Value.All(z => z != null)));
        }

        private static bool CheckDataCorrectness(Dictionary<ulong, TaskStatInfo> data)
        {
            return data.All(
                x => x.Key != null &&
                    x.Value.ResourceName != null &&
                    x.Value.ProcessInfoCollection.All(y => y.Key != null && y.Value != null && y.Value.All(z => z != null)));
        }

        private static bool CheckDataCorrectness(string fileName)
        {
            return !(string.IsNullOrEmpty(fileName) || string.IsNullOrWhiteSpace(fileName));
        }

        private static bool CheckDataCorrectness(DateTime startDate, DateTime endDate)
        {
            return startDate < endDate;
        }
    }
}
