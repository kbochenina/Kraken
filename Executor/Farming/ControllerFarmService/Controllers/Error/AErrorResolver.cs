using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ControllerFarmService.Controllers.Error
{
    public abstract class AErrorResolver
    {
        public static String SSH_ERROR = "ssh_error";
        public static String SSH_EXIT_CODE = "ssh_err_code";
        public static String SSH_COMMAND = "ssh_cmd";
        public static String SSH_RESULT = "ssh_res";
        
        public abstract string Resolve(IDictionary<String, Object> info);
    }
}
