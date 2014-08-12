using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.IO;

namespace MITP
{
    [DataContract]
    public enum TaskState
    {
        [EnumMember] Defined,
        [EnumMember] ReadyToExecute,
        [EnumMember] Scheduled,
        [EnumMember] Started,
        [EnumMember] Aborted,
        [EnumMember] Completed,
        [EnumMember] Failed
    }

    [DataContract]
    public enum TaskLaunchMode
    {
        [EnumMember] Auto,
        [EnumMember] Manual,
    }

    [DataContract]
    public enum TaskPriority
    {
        [EnumMember] Normal,
        [EnumMember] Urgent,
    }

    [DataContract]
    public struct TaskFileDescription
    {
        [DataMember] public string StorageId { get; set; }
        [DataMember] public string FileName  { get; set; }
        [DataMember] public string SlotName  { get; set; }
    }

    [DataContract]
    public class TaskDescription
    {
        [DataMember] public ulong  TaskId { get; protected set; }
        [DataMember] public string WfId   { get; protected set; }

        [DataMember] public string UserId   { get; protected set; }
        [DataMember] public string UserCert { get; protected set; }

        [DataMember] public TaskPriority   Priority   { get; protected set; }
        [DataMember] public TaskLaunchMode LaunchMode { get; protected set; }

        [DataMember] public string Package { get; protected set; }
        [DataMember] public string Method  { get; protected set; }

        [DataMember] public TaskFileDescription[] InputFiles  { get; protected set; }
        [DataMember] public TaskFileDescription[] OutputFiles { get; set; }

        [DataMember] public IDictionary<string, string> Params     { get; protected set; }
        [DataMember] public IDictionary<string, string> ExecParams { get; protected set; }

        public string ToJsonString()
        {
            var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(this.GetType());
            var memStream = new MemoryStream();
            serializer.WriteObject(memStream, (object) this);
            string json = Encoding.UTF8.GetString(memStream.ToArray());
            memStream.Close();

            return json;
        }

        private TaskDescription()
        {
            // Task Description should be constructed only from service users or from other Task Descriptions
        }

        public TaskDescription(TaskDescription other)
        {
            WfId = String.IsNullOrEmpty(other.WfId)? "0" : other.WfId;
            TaskId = other.TaskId;
            UserId = other.UserId; // todo : userId == null?
            UserCert = other.UserCert;

            Priority = other.Priority;
            LaunchMode = other.LaunchMode;

            Package = other.Package;
            Method = other.Method ?? "";

            if (other.Params != null)
                Params = new Dictionary<string, string>(other.Params);
            else
                Params = new Dictionary<string, string>();

            if (other.ExecParams != null)
                ExecParams = new Dictionary<string, string>(other.ExecParams);
            else
                ExecParams = new Dictionary<string, string>();

            if (other.InputFiles != null) // todo: inputs.ToArray()?
            {
                var inputsList = other.InputFiles.ToList();
                int len = inputsList.Count();
                InputFiles = new TaskFileDescription[len];

                for (int i=0; i<len; i++)
                {
                    InputFiles[i] = new TaskFileDescription
                    {
                        FileName  = inputsList[i].FileName,
                        SlotName  = inputsList[i].SlotName,
                        StorageId = inputsList[i].StorageId,
                    };
                }
            }
            else
                InputFiles = new TaskFileDescription[0]; // todo : is it ok?

            if (other.OutputFiles != null)
            {
                var outputsList = other.OutputFiles.ToList();
                int len = outputsList.Count();
                OutputFiles = new TaskFileDescription[len];

                for (int i=0; i<len; i++)
                {
                    OutputFiles[i] = new TaskFileDescription
                    {
                        FileName  = outputsList[i].FileName,
                        SlotName  = outputsList[i].SlotName,
                        StorageId = outputsList[i].StorageId,
                    };
                }
            }
            else
                OutputFiles = new TaskFileDescription[0]; // todo : is it ok?
        }
    }
}