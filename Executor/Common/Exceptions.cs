using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MITP
{
    public class PackageNotSupportedException : Exception
    {
        public PackageNotSupportedException()
            : base()
        {
        }

        public PackageNotSupportedException(string message)
            : base(message)
        {
        }
    }
}

