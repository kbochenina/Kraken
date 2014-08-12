using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MITP
{
    class Scheduler
    {
        public const double E = 1e-10;

        public const int ITERATIONS_PER_WORKFLOW = 101; // for statistics
        public const int WORKFLOWS_PER_PARAM_SET = 100; // for statistics

        public double NextRealTime(Task task, Resource resource, double systemMean, double systemDeviation)
        {
            return  
                      Math.Max(0, NextGauss(task.mean, task.deviation) / (1.0 * resource.performance))
                    + Math.Max(0, NextGauss(resource.mean, resource.deviation))  // from resource
                    + Math.Max(0, NextGauss(systemMean, systemDeviation)); // from system
        }

        public double Estimate(Task task, Resource resource)
        {
            return (task.mean + resource.mean) / (1.0 * resource.performance) + resource.estimatedAvailabilityTime;
        }


        #region Heuristics

        public delegate Task Heur(IEnumerable<Task>[] taskEstimations);

        public static Heur[] heuristicsToTest = new Heur[]
        {
            new Heur(MaxMinHeuristics),
            new Heur(MinMinHeuristics),
            new Heur(SufferageHeuristics),
        };
                                                                  

        public static Task DumbHeuristics(IEnumerable<Task>[] taskEstimations)
        {
            return taskEstimations[0].First();
        }

        public static Task MinMinHeuristics(IEnumerable<Task>[] taskEstimations)
        {
            double minTime = taskEstimations.Min(est => est.First().estimatedCompletionTime);
            var bestTaskEstimationList = taskEstimations.First(est => minTime == est.First().estimatedCompletionTime);
            var bestTaskOnResource = bestTaskEstimationList.First();
            return bestTaskOnResource;
        }

        public static Task MaxMinHeuristics(IEnumerable<Task>[] taskEstimations)
        {
            double maxTime = taskEstimations.Max(est => est.First().estimatedCompletionTime); // среди лучших ресурсов
            var bestTaskEstimationList = taskEstimations.First(est => maxTime == est.First().estimatedCompletionTime);
            var bestTaskOnResource = bestTaskEstimationList.First();
            return bestTaskOnResource;
        }

        public static Task SufferageHeuristics(IEnumerable<Task>[] taskEstimations)
        {
            double maxDiffTime = taskEstimations.Max(
                est => est.ElementAt(1).estimatedCompletionTime - est.First().estimatedCompletionTime
            );

            var bestTaskEstimationList = taskEstimations.First(
                est => maxDiffTime == est.ElementAt(1).estimatedCompletionTime - est.First().estimatedCompletionTime
            );

            var bestTaskOnResource = bestTaskEstimationList.First();
            return bestTaskOnResource;
        }


        #endregion

        #region Datatypes

        [DataContract]
        public struct Resource // == node
        {
            [DataMember] public double performance;

            public double estimatedAvailabilityTime;
            public double realAvailabilityTime;

            [DataMember] public double mean;
            [DataMember] public double deviation;

            public int currentTaskId;

            public bool IsAvailable()
            {
                return (realAvailabilityTime <= E);                     
            }
        }

        [DataContract]
        public struct Task
        {
            [DataMember(Order=0)] public int id;

            public int resourceId;
            public double estimatedCompletionTime;
            public double realCompletionTime;
            
            [DataMember] public double mean;
            [DataMember] public double deviation;

            public Task(Task other)
            {
                id = other.id;

                mean = other.mean;
                deviation = other.deviation;

                resourceId = other.resourceId;
                estimatedCompletionTime = other.estimatedCompletionTime;
                realCompletionTime = other.realCompletionTime;
            }
        }

        [DataContract]
        public struct Workflow
        {
            [DataMember] public string name;

            [DataMember] public double systemMean;
            [DataMember] public double systemDeviation;

            [DataMember] public Resource[] resources;

            [DataMember] public List<Task> readyTasks;
            public List<Task> launchedTasks;
            public List<Task> scheduledTasks;

            public Workflow(Workflow other)
            {
                resources = (Resource[]) other.resources.Clone();

                readyTasks = new List<Task>(other.readyTasks);
                launchedTasks = new List<Task>(other.launchedTasks);
                scheduledTasks = new List<Task>(other.scheduledTasks);

                name = other.name;
                systemMean = other.systemMean;
                systemDeviation = other.systemDeviation;
            }
        }

        #endregion

        #region Processing

        public Workflow Reschedule(Workflow workflow, Heur heuristics)
        {
            var wf = new Workflow(workflow);

            while (wf.readyTasks.Count > 0)
            {
                var estimatedTasks = new IEnumerable<Task>[wf.readyTasks.Count];

                for (int i=0; i<wf.readyTasks.Count; i++)
                {
                    estimatedTasks[i] = new List<Task>();
                    var task = wf.readyTasks[i];

                    for (int j=0; j<wf.resources.Length; j++)
                    {
                        var resource = wf.resources[j];
                        var taskOnResource = new Task(task);

                        taskOnResource.estimatedCompletionTime = Estimate(taskOnResource, resource);

                        taskOnResource.realCompletionTime = -1;
                        taskOnResource.resourceId = j;
                        
                        ((List<Task>) estimatedTasks[i]).Add(taskOnResource);
                    }

                    estimatedTasks[i] = estimatedTasks[i].OrderBy(e => e.estimatedCompletionTime);
                }

                var selectedTask = heuristics(estimatedTasks);
                int selectedResoucreId = selectedTask.resourceId;

                if (wf.resources[selectedResoucreId].IsAvailable())
                {
                    // launch selected task on selected resource

                    selectedTask.realCompletionTime = NextRealTime(selectedTask, wf.resources[selectedResoucreId], wf.systemMean, wf.systemDeviation);

                    wf.resources[selectedResoucreId].estimatedAvailabilityTime += selectedTask.estimatedCompletionTime;
                    wf.resources[selectedResoucreId].realAvailabilityTime += selectedTask.realCompletionTime;
                    wf.resources[selectedResoucreId].currentTaskId = selectedTask.id;

                    wf.readyTasks.RemoveAll(task => task.id == selectedTask.id);
                    wf.launchedTasks.Add(selectedTask);
                }
                else
                {
                    // schedule selected task on selected resource

                    wf.resources[selectedResoucreId].estimatedAvailabilityTime += selectedTask.estimatedCompletionTime;

                    wf.readyTasks.RemoveAll(task => task.id == selectedTask.id);
                    wf.scheduledTasks.Add(selectedTask);
                }
            }

            // возвращаяем все запланированные, но не запущенные задачи обратно в очередь -- а вдруг переназначим на другие ресурсы? Это актуально и потому, что могут придти новые задачи, и потому, что realTime != estimatedTime

            foreach (var task in wf.scheduledTasks)
                wf.resources[task.resourceId].estimatedAvailabilityTime -= task.estimatedCompletionTime;

            wf.readyTasks.AddRange(wf.scheduledTasks);
            wf.scheduledTasks.Clear();

            return wf;        
        }

        #endregion

        #region Ramdomness

        private static object _prngLock = new object();
        private static RandomOps.Random _prng = null;

        public static double NextGauss(double mean, double deviation)
        {
            lock (_prngLock)
            {                
                if (_prng == null)
                    InitPRNG();

                return _prng.Gauss(mean, deviation);
            }
        }

        public static double NextUniform(double l, double r)
        {
            lock (_prngLock)
            {
                if (_prng == null)
                    InitPRNG();

                return _prng.Uniform(l, r);
            }
        }

        public static void InitPRNG()
        {
            lock (_prngLock)
            {
                const int SEEDS_COUNT = 20;
                const int SEEDS_BYTES_COUNT = SEEDS_COUNT * sizeof(uint);

                byte[] trueRandomBytes = RandomOps.RandomDotOrg.RetrieveBytes(SEEDS_BYTES_COUNT).ToArray();

                var seeds = new List<uint>();
                for (int i=0; i<trueRandomBytes.Length; )
                {
                    uint seed = 0;

                    for (int j=0; j<sizeof(uint); j++)
                    {
                        seed = (seed << 8) + trueRandomBytes[i];
                        i++;
                    }

                    seeds.Add(seed);
                }

                _prng = new RandomOps.MersenneTwister(seeds.ToArray());
            }
        }

        public static string MakeHist(IEnumerable<double> data, double left, double right, int colCount)
        {
            left -= E;
            right += E;

            double h = (right - left) / (1.0 * colCount);
            double l = left;
            double r = left + h;

            double[] avg = new double[colCount];
            int[] amount = new int[colCount];
            int i = 0;

            while (l + E < right)
            {
                avg[i] = (l + r) / 2.0;
                if (h > 1) avg[i] = Math.Floor(avg[i]);

                foreach (double value in data)
                    amount[i] += (l <= value && value < r)? 1 : 0;

                l = r;
                r += h;
                i++;
            }

            string csvContent = "";
            csvContent += String.Join(";", avg.Select(n => n.ToString())) + Environment.NewLine;
            csvContent += String.Join(";", amount.Select(n => n.ToString()));

            return csvContent;
        }
        

        #endregion
    }
}
