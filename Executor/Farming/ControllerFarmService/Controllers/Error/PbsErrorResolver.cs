using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ControllerFarmService.Controllers.Error
{
    class PbsErrorResolver : AErrorResolver
    {
        public static int UNKNOWN_JOB_ID = 153;

        public override string Resolve(IDictionary<string, object> info)
        {
            var cmd = info[SSH_COMMAND].ToString();
            var err = info[SSH_ERROR].ToString();
            var err_code = int.Parse(info[SSH_EXIT_CODE].ToString());
            var result = info[SSH_RESULT].ToString();
            
                if (!String.IsNullOrWhiteSpace(err))
                {
                    if (int.Parse(info[SSH_EXIT_CODE].ToString()) == UNKNOWN_JOB_ID) 
                    {
                        return "job_state = C";
                    }
                    throw new Exception(String.Format("Ssh execution error. Command: \"{0}\". Code: {1}, StdOut: {2}, StdErr: {3}", cmd, err_code, result, err));
                }
            return result;
        }
    }
}
