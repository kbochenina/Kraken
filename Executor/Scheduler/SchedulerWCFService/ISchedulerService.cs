using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace SchedulerWCFService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "ISchedulerService" in both code and config file together.
    [ServiceContract]
    public interface ISchedulerService
    {
        [OperationContract]
        void DoWork();
    }
}
