using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

namespace MITP
{
	public abstract class Log
	{
		private const bool DEBUGGING = true;
		private const int MAX_STACK_DEPTH = 3;

		private const int MAX_TRIES_COUNT_TO_WRITE = 3;
		private const int TIME_TO_NEXT_TRY_TO_WRITE = 1000; // milliseconds

		private static object _logFileLock = new object();

		/// <summary>
		/// Перевести сообщение в формат журнала
		/// </summary>
		/// <param name="message">Сообщение</param>
		/// <returns>Строка журнала</returns>
		private static string ConvertToLogForm(string message)
		{
            //return message.Replace("\t", "    ").Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
			string logMessage = message;
			logMessage = logMessage.Replace("\t", "    ");
			logMessage = logMessage.Replace("\r\n", "\t");
			logMessage = logMessage.Replace("\n", "\t");
			logMessage = logMessage.Replace("\r", "\t");
			return logMessage;
		}

		/// <summary>
		/// Перевести строку журнала в читаемый формат
		/// </summary>
		/// <param name="logMessage">Строка журнала</param>
		/// <returns>Строки сообщения</returns>
		public static string[] ConvertFromLogForm(string logMessage)
		{
			try
			{
				string[] originalLines = logMessage.Split(new char[] { '\t' });
				return originalLines;
			}
			catch (Exception e)
			{
				Log.Warn(String.Format(
					"Не удалось перевести сообщения журнала из «сырой» формы: {0}\n{1}",
					e.Message, e.StackTrace
				));
				return new string[] { logMessage };
			}
		}

		/// <summary>
		/// Считать все сообщения журнала в «сыром» формате журнала
		/// </summary>
		/// <returns>Сообщения журнала в «сыром» формате самого журнала</returns>
		public static string[] LoadLogMessages()
		{
			lock (_logFileLock)
			{
				try
				{
					if (!File.Exists(CONST.Path.LogFile))
						return new string[] { "" };

					return File.ReadAllLines(CONST.Path.LogFile, Encoding.Default);
				}
				catch (Exception e)
				{
					Log.Warn(String.Format("Ошибка чтения журнала: {0}\n{1}", e.Message, e.StackTrace));
					return new string[] { };
				}
			}
		}

		/// <summary>
		/// Считать все сообщения журнала в читаемом виде одной строкой. Исключения перехватываются
		/// </summary>
		/// <returns>Строка со всеми сообщениями журнала в читаемом виде</returns>
		public static string LoadLogMessagesAsOneLineConverted()
		{
			lock (_logFileLock)
			{
				try
				{
					if (!File.Exists(CONST.Path.LogFile))
						return "";

					string logFileContent = File.ReadAllText(CONST.Path.LogFile, Encoding.Default);
					string[] convertedLines = ConvertFromLogForm(logFileContent);
					string oneLine = String.Join(Environment.NewLine, convertedLines);
					return oneLine;
				}
				catch (Exception e)
				{
					Log.Warn(String.Format("Ошибка чтения журнала: {0}\n{1}", e.Message, e.StackTrace));
					return "";
				}
			}
		}

		/// <summary>
		/// Непосредственная запись сообщения в файл журнала
		/// </summary>
		/// <param name="level">Уровень сообщения</param>
		/// <param name="message">Сообщение</param>
		private static void WriteMessage(string level, string message)
		{
			bool success = false;
            for (int tries = 0; tries < MAX_TRIES_COUNT_TO_WRITE && !success; tries++)
			{
				try
				{
					DateTime messageTime = DateTime.Now;

					string stackTrace = "";
					const int FROM_FRAME = 2;  // 0 - current method, 1 - method before etc.
					var trace = new System.Diagnostics.StackTrace();
					for (int frameNum = FROM_FRAME; frameNum < Math.Min(FROM_FRAME + MAX_STACK_DEPTH, trace.FrameCount); frameNum++)
					{
						var frame = trace.GetFrame(frameNum);

						var method = frame.GetMethod();
						Type methodType = method.ReflectedType;

						string curMethodInfo = "?";
                        if (methodType != null)                           
                            curMethodInfo = String.Format("{0}.{1}.{2}", methodType.Namespace, methodType.Name, method.Name);

						stackTrace += curMethodInfo + '\n';
					}

					if (FROM_FRAME + MAX_STACK_DEPTH < trace.FrameCount)
						stackTrace += "...";

                    //string line = String.Format("{3}\n{0:16}\n{1}\n{2}\n", level, message.Trim(), stackTrace, messageTime.ToString());
                    string line = String.Format("{3}\n{0} \n{1}\t\t{2}\n", level, message.Trim(), stackTrace, messageTime.ToString("dd.MM.yy HH:mm:ss.fff"));
                    line = ConvertToLogForm(line) + Environment.NewLine;

					lock (_logFileLock)
					{
						File.AppendAllText(CONST.Path.LogFile, line, Encoding.Default);
					}

					success = true;
				}
				catch
				{
					Thread.Sleep(TIME_TO_NEXT_TRY_TO_WRITE);
				}
			}
		}

		/// <summary>
		/// Записать в журнал сведения о фатальной ошибке, приводящей к краху приложения
		/// </summary>
		/// <param name="message">Сообщение</param>
		public static void Fatal(string message)
		{
            //WriteMessage("КРАХ", message);
            WriteMessage("FATAL", message);
        }

		/// <summary>
		/// Записать в журнал сведения о нефатальной ошибке
		/// </summary>
		/// <param name="message">Сообщение</param>
		public static void Error(string message)
		{
            //WriteMessage("ОШИБКА", message);
            WriteMessage("ERROR", message);
        }

		/// <summary>
		/// Записать в журнал сведения о потенциально опасной ситуации
		/// </summary>
		/// <param name="message">Сообщение</param>
		public static void Warn(string message)
		{
            //WriteMessage("ПРЕДУПРЕЖДЕНИЕ", message);
            WriteMessage("WARN", message);
        }

		/// <summary>
		/// Записать в журнал сведения о ходе выполнения программы
		/// </summary>
		/// <param name="message">Сообщение</param>
		public static void Info(string message)
		{
			//WriteMessage("Информация", message);
            WriteMessage("Info", message);
        }

		/// <summary>
		/// Записать в журнал сведения, необходимые только для отладки
		/// </summary>
		/// <param name="message">Сообщение</param>
		public static void Debug(string message)
		{
			if (Log.DEBUGGING)
			{
                //WriteMessage("Отладка", message);
                WriteMessage("Debug", message);
            }
		}

		public static void Stats(string name, string wfId, ulong taskId, TimeSpan time)
		{
            /*
			WriteMessage("Статистика", String.Format("{0} {1} {2} {3}",
				wfId, taskId, name,
				String.Format("{0} {1}:{2}:{3}.{4}",
					time.Days.ToString("00"),
					time.Hours.ToString("00"),
					time.Minutes.ToString("00"),
					time.Seconds.ToString("00"),
					time.Milliseconds.ToString("000")
				)
			));
            */
		}

        public static void Stats(string name, string wfId, ulong taskId, DateTime time)
		{
            /*
			WriteMessage("Статистика", String.Format("{0} {1} {2} {3}",
				wfId, taskId, name, DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fff")
			));
            */
		}

        public static void Over(string mess, params object[] args)
        {
            lock (_logFileLock)
            {
                File.AppendAllText(
                    CONST.Path.OverFile, 
                    String.Format(mess + Environment.NewLine + Environment.NewLine, args), 
                    Encoding.Default
                );
            }
        }
	}
}


