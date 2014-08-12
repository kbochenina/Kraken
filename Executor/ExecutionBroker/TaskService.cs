using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Collections;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace MITP
{
	[WebService(Namespace = "http://localhost/MITP/TaskManager")]
	[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
	[System.ComponentModel.ToolboxItem(false)]
	public class TaskService : System.Web.Services.WebService
	{
		private static object _taskServiceLock = new object();

		/// <summary>
		/// Посчитать приблизительное время выполнения задачи с указанными параметрами
		/// </summary>					 
		/// <param name="Package">Программа, используемая для счета</param>
		/// <param name="LaunchMode">Тип запуска (ручной или через экспертную систему)</param>
		/// <param name="InputFiles">Список описаний входных файлов</param>
		/// <param name="OutputFiles">Список описаний выходных файлов</param>
		/// <param name="ParamKeys">Список ключей параметров</param>
		/// <param name="ParamValues">Список значений параметров</param>
		/// <returns>Приблизительное время выполнения задачи (в секундах)</returns>
		[WebMethod]
		public double EstimateTime(Packs Package, CONST.LaunchMode LaunchMode, TaskFileDescription[] InputFiles, TaskFileDescription[] OutputFiles, string[] ParamKeys, string[] ParamValues)
		{
			string taskId = TaskCreate(0, Package, LaunchMode, InputFiles, OutputFiles, ParamKeys, ParamValues);
			return TaskEstimateTime(taskId);
		}

		/// <summary>
		/// Посчитать приблизительное время выполнения созданной ранее задачи в текущих условиях.
		/// </summary>
		/// <param name="TaskId">Идентификатор задачи (строка)</param>
		/// <returns>Приблизительное время выполнения задачи (в секундах)</returns>
		[WebMethod]
		public double TaskEstimateTime(string TaskId)
		{
			lock (_taskServiceLock)
			{
				Task task = TaskFromId(TaskId);
				task.AutoAssign();

				if (!task.Time.Estimated.HasValue)
					throw new Exception("Time estimation error!");
				 
				task.Save();

				return task.Time.Estimated.Value.TotalSeconds;
			}
		}


		/// <summary>
		/// Создать задачу по указанным параметрам
		/// </summary>
		/// <param name="SequenceId">Идентификатор последовательности, к которой относится задача</param>
		/// <param name="Package">Программа, используемая для счета</param>
		/// <param name="LaunchMode">Тип запуска (ручной или через экспертную систему)</param>
		/// <param name="InputFiles">Список описаний входных файлов</param>
		/// <param name="OutputFiles">Список описаний выходных файлов</param>
		/// <param name="ParamKeys">Список ключей параметров</param>
		/// <param name="ParamValues">Список значений параметров</param>
		/// <returns>Идентификатор задачи (строка)</returns>
		[WebMethod]
		public string TaskCreate(ulong SequenceId, Packs Package, CONST.LaunchMode LaunchMode, TaskFileDescription[] InputFiles, TaskFileDescription[] OutputFiles, string[] ParamKeys, string[] ParamValues)
		{
			lock (_taskServiceLock)
			{
				Task task = new Task(SequenceId, Package, LaunchMode, InputFiles, OutputFiles);

				if (ParamKeys != null && ParamKeys.Length > 0)
				{
					if (ParamKeys.Length != ParamValues.Length)
						throw new Exception("Param keys and values lengths must be equal!");

					int length = ParamKeys.Length;
					for (int i = 0; i < length; i++)
						task.Params[ParamKeys[i]] = ParamValues[i];
				}

				Log.Debug(String.Format(
					"Цепочка {0}: создана задача {1}", SequenceId, task.TaskId
				));
				task.Save();

				return task.TaskId;
			}
		}

		/// <summary>
		/// Запустить созданную ранее задачу на свободном вычислителе
		/// </summary>
		/// <param name="TaskId">Идентификатор задачи (строка)</param>
		[WebMethod]
		public void TaskRun(string TaskId)
		{
			lock (_taskServiceLock)
			{
				try
				{
					Task task = Task.Load(TaskId);

					Log.Stats("T_task_start", task.SequenceId, task.TaskId, DateTime.Now);

					if (task.AssignedTo == null || String.IsNullOrEmpty(task.AssignedTo.ClusterName))
					{
						DateTime timeBefore = DateTime.Now;

						task.AutoAssign();

						Log.Stats("T_assign", task.SequenceId, task.TaskId, DateTime.Now - timeBefore);
					}

					if (!task.Time.Estimated.HasValue)
						throw new Exception("Could not assign task to any cluster!");

					task.Run();
					task.Save();
				}
				catch (Exception e)
				{
					Log.Error(String.Format(
						"Задача {0}, ошибка при запуске: {1}\n{2}",
						TaskId, e.Message, e.StackTrace
					));

					TaskFail(TaskId);
				}
			}
		}

		/// <summary>
		/// Остановить выполнение запущенной задачи
		/// </summary>
		/// <param name="TaskId">Идентификатор задачи (строка)</param>
		[WebMethod]
		public void TaskAbort(string TaskId)
		{
			lock (_taskServiceLock)
			{
				try
				{
					Task task = Task.Load(TaskId);
					task.Abort();
					task.Save();

					var sequenceService = new SequenceService();
					sequenceService.StepFinishedCallback(task.SequenceId);
				}
				catch (Exception e)
				{
					Log.Error(String.Format(
						"Задача {2}, возникло исключение при попытке принудительного завершения\n{0}\n{1}", e.Message, e.StackTrace, TaskId
					));
				}
			}
		}

        /// <summary>
        /// Сообщить об успешном окончании работы запущенной задачи
        /// </summary>
        /// <param name="TaskId">Идентификатор задачи (строка)</param>
        [WebMethod]
        public void TaskComplete(string TaskId)
        {
            lock (_taskServiceLock)
            {
                Log.Info("Задача " + TaskId + ", получено сообщение о завершении");

                const int ATTEMPTS_MAX = 3;
                bool success = false;
                var rnd = new Random();

                for (int i = 0; i < ATTEMPTS_MAX && !success; i++)
                {
                    try
                    {
                        Task task = Task.Load(TaskId);
                        Log.Stats("T_clust_fin", task.SequenceId, task.TaskId, DateTime.Now);

                        task.Complete();
                        success = true;
                        task.Save();

                        Log.Stats("T_task_fin", task.SequenceId, task.TaskId, DateTime.Now);

                        if (task.SequenceId > 0)
                        {
                            var sequenceService = new SequenceService();
                            sequenceService.StepFinishedCallback(task.SequenceId);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(String.Format(
                            "Задача {2}, возникло исключение при завершении\n{0}\n{1}", e.Message, e.StackTrace, TaskId
                        ));
                    }

					
                    Thread.Sleep(rnd.Next(2*1000), 10*1000);
                }

                if (!success)
                    TaskFail(TaskId);
            }
        }

		/// <summary>
		/// Сообщить о неуспешном окончании работы запущенной задачи
		/// </summary>
		/// <param name="TaskId">Идентификатор задачи (строка)</param>
		[WebMethod]
		public void TaskFail(string TaskId)
		{
			lock (_taskServiceLock)
			{
				try
				{
					Task task = Task.Load(TaskId);
					task.Fail();
					task.Save();

					var sequenceService = new SequenceService();
					sequenceService.StepFinishedCallback(task.SequenceId);
				}
				catch (Exception e)
				{
					Log.Error(String.Format(
						"Задача {2}, возникло исключение при завершении со статусом Fail\n{0}\n{1}", e.Message, e.StackTrace, TaskId
					));
				}
			}
		}

		public struct TaskInfo
        {
            public TaskPossibleStates State;
			public string ClusterName;
			public uint NodesCount;
			public uint CoresCount;
			public string Package;

			public double TimeElapsed;

			public TaskFileDescription[] OutputFiles;
        }
        
        /// <summary>
		/// Получить информацию о задаче и ее состоянии
		/// </summary>
		/// <param name="TaskId">Идентификатор задачи (строка)</param>
		/// <returns>Информация о задаче и ее состоянии</returns>
		[WebMethod]
        public TaskInfo TaskGetInfo(string TaskId)
		{
			lock (_taskServiceLock)
			{
				Task task = Task.Load(TaskId);

				TaskInfo taskInfo = new TaskInfo();
				taskInfo.State = task.State;

				if (task.AssignedTo != null && !String.IsNullOrEmpty(task.AssignedTo.ClusterName))
				{
					taskInfo.ClusterName = task.AssignedTo.ClusterName;
					taskInfo.NodesCount = (uint) task.AssignedTo.Cores.Count(coresOnNode => coresOnNode > 0);
					taskInfo.CoresCount = (uint) task.AssignedTo.Cores.Sum();
					taskInfo.Package = task.Package.ToString();
				}
				else
				{
					taskInfo.ClusterName = null;
					taskInfo.NodesCount = 0;
					taskInfo.CoresCount = 0;
					taskInfo.Package = null;
				}

				if (task.Time.Ended.HasValue && task.Time.Started.HasValue)
					taskInfo.TimeElapsed = (task.Time.Ended.Value - task.Time.Started.Value).TotalSeconds;
				else
				if (task.Time.Started.HasValue)
					taskInfo.TimeElapsed = (DateTime.Now - task.Time.Started.Value).TotalSeconds;
				else
					taskInfo.TimeElapsed = 0;

				/*** return output files ***/
				if (task.OutputFiles != null && task.OutputFiles.Length > 0)
				{
					taskInfo.OutputFiles = new TaskFileDescription[task.OutputFiles.Length];

					for (int i = 0; i < task.OutputFiles.Length; i++)
					{
						taskInfo.OutputFiles[i].fileName = task.OutputFiles[i].fileName;
						taskInfo.OutputFiles[i].slotName = task.OutputFiles[i].slotName;
						taskInfo.OutputFiles[i].storageId = task.OutputFiles[i].storageId;
					}
				}
				else
					taskInfo.OutputFiles = null;

				return taskInfo;
			}
		}
	}
}
