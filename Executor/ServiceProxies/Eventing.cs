using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceProxies.EventingService;
using System.Xml.Serialization;

namespace MITP
{
    public static class Eventing
    {
        public enum EventType
        {
            TaskStarted,
            TaskCompleted,
            TaskFailed,
        }

        public static bool Send(EventType eventType, string wfId, ulong taskId, string comment = "")
        {
            var eventing = Discovery.GetEventingService();

            try
            {
                WFStateUpdatedEvent wfEvent = new WFStateUpdatedEvent();
                wfEvent.WFStepCode = taskId.ToString();
                wfEvent.WFRunCode = wfId;
                wfEvent.Comment = comment;

                switch (eventType)
                {
                    case EventType.TaskStarted:
                        wfEvent.WFStateUpdatedType = WFStateUpdatedTypeEnum.WFStepStarted;
                        break;
                    case EventType.TaskCompleted:
                        wfEvent.WFStateUpdatedType = WFStateUpdatedTypeEnum.WFStepFinished;
                        break;
                    case EventType.TaskFailed:
                        wfEvent.WFStateUpdatedType = WFStateUpdatedTypeEnum.WFStepError;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("eventType");
                }

                EventReport eventArgs = new EventReport()
                {
                    Source = "Execution",
                    Body =  Easis.Eventing.EventReportSerializer.SerializeObject(wfEvent, typeof(WFStateUpdatedEvent)),
                    SchemeUri = "http://escience.ifmo.ru/easis/eventing/schemes/WFStateUpdatedEvent.xsd",
                    Timestamp = DateTime.Now,
                    Topic = "WFStateUpdatedEvent"
                };

                eventing.FireEvent(eventArgs);
                eventing.Close();
                
                Log.Debug(String.Format("Event sent: {0}. WfId = {1}, TaskId = {2}", eventType.ToString(), wfId, taskId));
                return true;
            }
            catch (Exception e)
            {
                eventing.Abort();

                Log.Warn(String.Format("Event was NOT sent: {0}, WfId = {1}, TaskId = {2}: {3}", 
                    eventType.ToString(), wfId, taskId, e.ToString()
                ));
                return false;
            }
        }
    }
}
