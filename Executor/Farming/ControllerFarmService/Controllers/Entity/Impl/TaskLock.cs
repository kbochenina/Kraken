using System;

namespace MITP.Entity
{
    public class TaskLock
    {
        public static int NO_OPERATION_EXECUTED = 0;
        public static int READ_OPERATION_EXECUTED = 1;
        public static int WRITE_OPERATION_EXECUTED = 2;
        
        public static int WAIT_IN_MILLISECONDS = 120000;

        public static bool LOCK_HOLDER = true;
        public static bool LOCK_READER = false;

        public int State { get; set; }
        public DateTime Time { get; set; }

    }
}