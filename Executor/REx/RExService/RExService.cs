using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.IO;
using System.Threading;
using System.Diagnostics;
using Config = System.Web.Configuration.WebConfigurationManager;

namespace RExService
{
    public class RExService : IRExService
    {
        private static readonly TimeSpan JOB_FILE_CHECK_INTERVAL     = TimeSpan.FromSeconds(0.3);
        private static readonly TimeSpan JOB_FILE_CHECK_MAX_INTERVAL = TimeSpan.FromSeconds(3);

        #region IRExService Members

        public int Exec(ulong taskId)
        {
            try
            {
                string jobFilePath = Config.AppSettings["JobsFolder"] + taskId + @"\job_" + taskId + ".cmd";

                var whenStarted = DateTime.Now;
                while (!File.Exists(jobFilePath) && DateTime.Now < whenStarted + JOB_FILE_CHECK_MAX_INTERVAL)
                {
                    Console.WriteLine("Job file not found! Id = " + taskId.ToString());
                    Thread.Sleep(JOB_FILE_CHECK_INTERVAL);
                }

                string jobBatchContent = File.ReadAllText(jobFilePath);
                Console.WriteLine("Batch content for id = " + taskId.ToString() + ": " + Environment.NewLine + jobBatchContent);

                string bin = "cmd.exe";
                string args = "/c " + jobFilePath;

                var procInfo = new ProcessStartInfo(bin, args);
                //procInfo.UseShellExecute = true;
                var proc = Process.Start(procInfo);
                int pid = proc.Id;

                Console.WriteLine("Task = {0}, pid = {1}", taskId, pid);

                return pid;
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    "Exception while starting task {0}: {1}{3}{2}",
                    taskId, e.Message, e.StackTrace, Environment.NewLine
                );

                throw;
            }
        }

        public bool IsProcessRunning(int pid)
        {
            bool isRunning = false;

            try
            {
                var process = Process.GetProcessById(pid);
                //Console.WriteLine("Debug: " + process.ProcessName);

                if (process.ProcessName.ToLowerInvariant().Equals("cmd"))
                {
                    isRunning = true;
                    Console.WriteLine("{0}: pid = {1}, running", DateTime.Now, pid);
                }
                else
                    Console.WriteLine("{0}: pid = {1}, running but not cmd.exe", DateTime.Now, pid);
            }
            catch (ArgumentException)
            {
                // no such process
                Console.WriteLine("{0}: pid = {1}, doesn't exist", DateTime.Now, pid);
                isRunning = false;
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    "{4}: Exception in getting process {0} info: {1}{3}{2}", 
                    pid, e.Message, e.StackTrace, Environment.NewLine, DateTime.Now
                );

                throw;
            }

            return isRunning;
        }

        #endregion
    }
}
