using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace Scheduler
{
    [ServiceBehavior(Namespace = "http://escience.ifmo.ru/nano/services/", InstanceContextMode = InstanceContextMode.Single)]
    public class SchedulerService : ISchedulerService
    {
       #region ISchedulerService Members

        public Estimation GetCurrentEstimation(string taskId)
        {
            throw new NotImplementedException();
        }

        public Schedule ReSchedule()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
