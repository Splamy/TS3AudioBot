using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3AudioBot
{
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
	sealed class InfoAttribute : Attribute
	{
		public string Description { get; private set; }
		public InfoAttribute(string description)
		{
			Description = description;
		}
	}
}
