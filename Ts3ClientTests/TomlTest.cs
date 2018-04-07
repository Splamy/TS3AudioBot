using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nett;

namespace Ts3ClientTests
{
	class TomlTest
	{
		static void Main(string[] args)
		{
			var toml = Toml.ReadFile("conf.toml");

			var struc = toml.Get<TStruc>();

			Toml.WriteFile(toml, "conf_out.toml");
		}
	}

	class TStruc
	{
		public TKey main { get; set; }
		public TKey second { get; set; }
	}

	class TKey
	{
		public string key { get; set; }
	}
}
