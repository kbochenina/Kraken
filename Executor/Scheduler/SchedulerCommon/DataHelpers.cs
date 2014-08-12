using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.ServiceModel;

namespace Common
{
    [DataContract]
    public struct ErrorMessage
    {
        public ErrorMessage(ErrorCode code, string message)
            : this()
        {
            Code = code;
            Message = message;
        }

        [DataMember]
        public ErrorCode Code { get; private set; }

        [DataMember]
        public string Message { get; private set; }
    }

    [DataContract]
    public enum ErrorCode
    {
        [EnumMember]
        UNKNOWN,
        [EnumMember]
        ACCESS,
        [EnumMember]
        IO,
        [EnumMember]
        DATABASE,
        [EnumMember]
        ARGUMENT,
        [EnumMember]
        CONFIGURATION
    }

    [DataContract]
    public class RemoteException : FaultException<ErrorMessage>
    {
        public RemoteException(ErrorCode code, string message, string reason) : base(new ErrorMessage(code, message), new FaultReason(reason)) { }
    }
}
