// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3Client
{
	using System.Threading.Tasks;
	using Full;
	using static System.Console;

	static class DebugTests
	{
		static Ts3BaseClient fc = new Ts3FullClient(EventDispatchType.ExtraDispatchThread);
		static bool init = false;

		static void Main(string[] args)
		{
			fc = new Ts3FullClient(EventDispatchType.ExtraDispatchThread);
			fc.OnConnected += (s, e) => WriteLine("Connected");
			fc.OnDisconnected += (s, e) => WriteLine("Disconnected");

			FindRareBug();
			ReadLine();
			return;


			Task.Run(() => fc.Connect(new ConnectionDataFull
			{
				Username = "HAAAX",
				Hostname = "127.0.0.1",
				Port = 9987,
				Identity = Ts3Crypt.LoadIdentity("MG8DAgeAAgEgAiEA76LIMLxiti7JTkl4yeNRPiApiGyIRqF9km3ByalVZd8CIQDGz9jUYZIXgkSsyCYVywl0HTKoP+0Ch8OG+ia4boW0UAIgSY/aeQNjq0ryRiaifd6SMKbG9+KuoN/oXEu/lyr+SNg=", 57451630, 57451630),
			}));
			WriteLine("Running");
			ReadLine();
			WriteLine("Request stop");
			fc.Disconnect();
			WriteLine("REquest done");
			ReadLine();
		}

		static void FindRareBug()
		{
			if (!init)
			{
				init = true;
				fc.OnConnected += (s, e) =>
				{
					fc.Disconnect();
				};
				fc.OnDisconnected += (s, e) =>
				{
					System.Threading.Thread.Sleep(5000);
					FindRareBug();
				};
			}

			Task.Run(() =>
			{
				WriteLine("Conn Task");
				fc.Connect(new ConnectionDataFull
				{
					Username = "HAAAX",
					Hostname = "127.0.0.1",
					Port = 9987,
					Identity = Ts3Crypt.LoadIdentity("MG8DAgeAAgEgAiEA76LIMLxiti7JTkl4yeNRPiApiGyIRqF9km3ByalVZd8CIQDGz9jUYZIXgkSsyCYVywl0HTKoP+0Ch8OG+ia4boW0UAIgSY/aeQNjq0ryRiaifd6SMKbG9+KuoN/oXEu/lyr+SNg=", 57451630, 57451630),
				});
				//fc.Connect(new ConnectionData
				//{
				//	Username = "HAAAX",
				//	Hostname = "127.0.0.1",
				//	Port = 9987,
				//});
			});
		}
	}
}
