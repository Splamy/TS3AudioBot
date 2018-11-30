using System;
using TS3Client;
using TS3Client.Commands;
using TS3Client.Helper;
using TS3Client.Messages;
using TS3Client.Query;

namespace Ts3ClientTests
{
	public static class QueryTest
	{
		public static void Main(string[] args)
		{
			var query = new Ts3QueryClient(EventDispatchType.DoubleThread);
			var con = new ConnectionData() { Address = "127.0.0.1" };
			query.Connect(con);
			var use = query.UseServer(1);
			Console.WriteLine("Use: {0}", use.Ok);
			var who = query.WhoAmI();
			Console.WriteLine("Who: {0}", who.Ok ? (object)who.Value : who.Error.ErrorFormat());

			while (true)
			{
				var line = Console.ReadLine();
				if (string.IsNullOrEmpty(line))
					break;
				var dict = query.SendCommand<ResponseDictionary>(new Ts3RawCommand(line));
				if (dict.Ok)
				{
					foreach (var item in dict.Value)
					{
						foreach (var val in item)
						{
							Console.Write("{0}={1}", val.Key, val.Value);
						}
						Console.WriteLine();
					}
				}
				else
				{
					Console.WriteLine(dict.Error.ErrorFormat());
				}
			}
		}
	}
}
