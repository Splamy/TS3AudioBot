using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;

namespace TS3AudioBot
{
	class PluginManager : IDisposable
	{
		public PluginManager()
		{

		}

		public void LoadPlugin(Assembly assembly)
		{
			throw new NotImplementedException();
		}

		private void RegisterEvents()
		{
			throw new NotImplementedException();
		}

		private void RegisterCommands()
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
		}
	}

	public interface ITS3ABPlugin : IDisposable
	{
		IEnumerable<BotCommand> GetCommands();
		void Initialize(MainBot bot);
	}
}
