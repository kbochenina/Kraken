using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;

namespace MITP
{
	public sealed class ReflectedList<RefType> : List<RefType>
	{
		private void Reflect()
		{
            var assembly = Assembly.GetAssembly(typeof(RefType));
            Type[] types = assembly.GetExportedTypes();

			foreach (Type type in types)
			{
				if (type.IsSubclassOf(typeof(RefType)) && !type.IsAbstract)
				{
					RefType member = (RefType)type.GetConstructor(Type.EmptyTypes).Invoke(null);
					Add(member);
				}
			}
		}

		public ReflectedList() : base()
		{
			Reflect();
		}
	}
}


