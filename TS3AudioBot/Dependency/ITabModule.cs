using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3AudioBot.Dependency
{
	public interface ITabModule
	{
		void Initialize();
	}

	public interface ICoreModule : ITabModule
	{
	}

	public interface IBotModule : ICoreModule
	{
	}
}
