using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MITP
{
    public class TimeLimitException : Exception
    {
        public TimeLimitException()
            : base("Time limit exceeded")
        {
        }

        public TimeLimitException(string message)
            : base(message)
        {
        }
    }

    public partial class Tools
    {
        private const int MAX_THREADS_FOR_TIMELIMITED_PROCESSING = 30;
        private static SemaphoreSlim _timeLimitSemaphore = new SemaphoreSlim(MAX_THREADS_FOR_TIMELIMITED_PROCESSING, MAX_THREADS_FOR_TIMELIMITED_PROCESSING);

        public static void ProcessWithTimeLimit(TimeSpan timeLimit, Action action)
        {
            try
            {
                _timeLimitSemaphore.Wait();

                Exception threadException = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception e)
                    {
                        Log.Warn("Exception while processing something with time limit: " + e.ToString());
                        threadException = e;
                    }
                });

                thread.Start();
                bool inTime = thread.Join(timeLimit);

                if (!inTime)
                {
                    thread.Abort();
                    throw new TimeLimitException();
                }
                else
                {
                    if (threadException != null)
                        throw threadException;
                }
            }
            finally
            {
                _timeLimitSemaphore.Release();
            }
        }
    }
}
