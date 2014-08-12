using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.ServiceModel;
using Config = System.Configuration.ConfigurationManager;

namespace ControllerFarmHost
{
    public class Indicators
    {
        private static object _titleLock = new object();
        private static bool _isBeating = false;
        private static string[] _resourceNames = new string[0];
        private static ulong[] _taskIds = new ulong[0];

        public static bool IsBeating { set { lock (_titleLock) _isBeating = value; UpdateTitle(); } }
        public static string[] ResourceNames { set { lock (_titleLock) _resourceNames = value; UpdateTitle(); } }
        public static ulong[] TaskIds { set { lock (_titleLock) _taskIds = value; UpdateTitle(); } }

        public static void UpdateTitle()
        {
            int NUMBER_OF_TASKS_TO_SHOW = 5;

            lock (_titleLock)
            {
                string title = String.Format("{0}{1} -- {2} -- {4} task{6}: {3}{5}",
                    _isBeating ? "*" : " ",
                    Program.GetFarmId(),
                    String.Join(" ", _resourceNames.OrderBy(name => name)),
                    String.Join(" ", _taskIds.Select(n => n.ToString()).Take(NUMBER_OF_TASKS_TO_SHOW)),
                    _taskIds.Length,
                    (_taskIds.Length > NUMBER_OF_TASKS_TO_SHOW) ? "..." : "",
                    (_taskIds.Length /* % 10 */== 1)? "": "s" // todo: check english grammar
                ).TrimEnd(new[] { ' ', ':' });

                Console.Title = title;
            }
        }
    }

    public class Program
    {
        public const string FARMID_PARAM_NAME = "FarmId";
        public static string GetFarmId()
        {
            return Config.AppSettings[FARMID_PARAM_NAME];
        }

        private static void LogToConsole(string format, params object[] args)
        {
            string time = DateTime.Now.ToString("dd.MM H:mm:ss.fff") + ": ";
            Console.WriteLine(time + format, args);
        }

        private static void LogToConsole(object arg)
        {
            LogToConsole("{0}", arg);
        }

        //const string TIME_FORMAT = "H:mm:ss.fff";
        const int TICK = 050; // minimum time to heartbeat in milliseconds
        const int TACK = 150; // maximum time to heartbeat in milliseconds

        public static ControllerFarmService.ControllerFarmServiceClient GetFarmService()
        {
            var service = new ControllerFarmService.ControllerFarmServiceClient("WSHttpBinding_IControllerFarmService");
            return service;
        }

        static void FarmServiceHost()
        {
            Thread.Sleep(TimeSpan.FromSeconds(1));

            while (true)
            {
                using (var host = new ServiceHost(typeof(MITP.ControllerFarmService)))//, new Uri("http://localhost:5555")))
                {
                    try
                    {
                        host.Open();

                        var serviceUri = host.BaseAddresses.First();
                        LogToConsole("Service started at {0}", serviceUri);

                        Console.ReadLine();
                    }
                    catch (Exception e)
                    {
                        LogToConsole("Error in service host: {0}", e);
                        Console.WriteLine();
                        Console.WriteLine("Restarting...");
                        Thread.Sleep(5000);
                    }
                    finally
                    {
                        host.Close();
                    }
                }
            }
        }

        enum ServiceToBeat
        {
            Executor,
            ResourceBase
        }
        
        static void Heartbeat(ServiceToBeat serviceType)
        {
            Thread.Sleep(TimeSpan.FromSeconds(10));
            var rnd = new System.Random();

            while (true)
            {
                try
                {
                    // todo : heartbeat for each service in config

                    if (serviceType == ServiceToBeat.Executor)
                    {
                        var executor = new ExecutionBrokerService.ExecutionBrokerServiceClient();
                        try
                        {

                            //Console.Write("{0} - ", DateTime.Now.ToString(TIME_FORMAT));
                            Indicators.IsBeating = true;
                            executor.Update();
                            Indicators.IsBeating = false;
                            //Console.WriteLine(DateTime.Now.ToString(TIME_FORMAT));

                            executor.Close();
                        }
                        catch
                        {
                            Indicators.IsBeating = false;

                            executor.Abort();
                            throw;
                        }
                    }
                    else
                    if (serviceType == ServiceToBeat.ResourceBase)
                    {
                        var resourceBase = new ResourceBaseService.ResourceBaseServiceClient();
                        try
                        {
                            resourceBase.GetResourceNames(); // only need to call to refresh resources, no need in result
                            resourceBase.Close();
                        }
                        catch
                        {
                            resourceBase.Abort();
                            throw;
                        }
                    }
                }
                catch (Exception e)
                {
                    //Indicators.IsBeating = false;
                    LogToConsole(e);
                    Thread.Sleep(5000);
                }
                finally
                {
                    //Indicators.IsBeating = false;
                    Thread.Sleep(rnd.Next(TICK, TACK));
                }
            }
        }

        static void UpdateResources()
        {
            Thread.Sleep(TimeSpan.FromSeconds(8));

            while (true)
            {
                try
                {
                    var service = GetFarmService();

                    try
                    {
                        string[] names = service.GetActiveResourceNames();

                        Indicators.ResourceNames = names;
                        foreach (string name in names)
                        {
                            var nodesState = service.GetNodesState(name);
                            var downNodes = nodesState.Where(n => n.State == ControllerFarmService.NodeState.Down);

                            foreach (var node in downNodes)
                                LogToConsole("{0}.{1} is down", node.ResourceName, node.NodeName);
                        }

                        service.Close();
                    }
                    catch
                    {
                        service.Abort();
                        throw;
                    }
                }
                catch (Exception e)
                {
                    LogToConsole(e);
                }
                finally
                {
                    Thread.Sleep(700);
                }
            }                       
        }

        static void UpdateTasks()
        {
            Thread.Sleep(TimeSpan.FromSeconds(7));

            while (true)
            {
                try
                {
                    var service = GetFarmService();

                    try
                    {
                        ulong[] ids = service.GetActiveTaskIds();

                        Indicators.TaskIds = ids;
                        foreach (ulong id in ids)
                        {
                            var taskState = service.GetTaskStateInfo(id);
                        }
                        /*
                        string tasksList = String.Format("{0} active tasks: {1}", ids.Length, String.Join(", ", ids.Select(i => i.ToString())));
                        Console.Title = title_start + " " + tasksList;
                        if (ids.Length > 0)
                            LogToConsole(tasksList);
                        */

                        service.Close();
                    }
                    catch
                    {
                        service.Abort();
                        throw;
                    }
                }
                catch (Exception e)
                {
                    LogToConsole(e);
                }
                finally
                {
                    Thread.Sleep(200);
                }
            }                       
        }

        static void InitConsoleWindow()
        {
            try
            {
                Indicators.UpdateTitle();

                if (Console.WindowWidth == 80) // standart => not specified in link's properties
                    Console.WindowWidth = 110; // to hold whole title

                if (Console.WindowHeight == 25)
                    Console.WindowHeight = 35;
            }
            catch (Exception e)
            {
                LogToConsole(e.Message);
            }
        }

        static void Main(string[] args)
        {
            InitConsoleWindow();

            new Thread(() => FarmServiceHost()).Start();

            new Thread(() => Heartbeat(ServiceToBeat.Executor)).Start();
            new Thread(() => Heartbeat(ServiceToBeat.ResourceBase)).Start();
            new Thread(() => UpdateResources()).Start();
            new Thread(() => UpdateTasks()).Start();
        }
    }
}
