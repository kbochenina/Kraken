using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MITP
{
    class TaskManager
	{
		[ThreadStatic]
		private static ReflectedList<PackageAdapter> _adapters;

		public static ReflectedList<PackageAdapter> Adapters
		{
			get
			{
				if (_adapters == null)
					_adapters = new ReflectedList<PackageAdapter>();

				return _adapters;
			}
		}

		[ThreadStatic]
		private static ReflectedList<LaunchModel> _launchModels;

		public static ReflectedList<LaunchModel> LaunchModels
		{
			get
			{
				if (_launchModels == null)
					_launchModels = new ReflectedList<LaunchModel>();

				return _launchModels;
			}
		}
	}
}