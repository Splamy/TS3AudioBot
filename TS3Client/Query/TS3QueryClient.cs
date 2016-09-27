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

namespace TS3Client.Query
{
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net.Sockets;

	public class TS3QueryClient : TS3BaseClient
	{
		private Queue<WaitBlock> requestQueue = new Queue<WaitBlock>();
		private TcpClient tcpClient;
		private NetworkStream tcpStream;
		private StreamReader tcpReader;
		private StreamWriter tcpWriter;

		// CTORS

		public TS3QueryClient(EventDispatchType dispatcher) : base(dispatcher)
		{
			tcpClient = new TcpClient();
		}

		// METHODS

		protected override void ConnectInternal(ConnectionData conData)
		{
			try { tcpClient.Connect(conData.Hostname, conData.Port); }
			catch (SocketException ex) { throw new TS3CommandException(new CommandError(), ex); }

			tcpStream = tcpClient.GetStream();
			tcpReader = new StreamReader(tcpStream);
			tcpWriter = new StreamWriter(tcpStream) { NewLine = "\n" };

			for (int i = 0; i < 3; i++)
				tcpReader.ReadLine();
		}

		protected override void DisconnectInternal()
		{
			status = TS3ClientStatus.Quitting;
			tcpWriter.WriteLine("quit");
			tcpWriter.Flush();
		}

		protected override void NetworkLoop()
		{
			string dataBuffer = null;

			while (true)
			{
				string line;
				try { line = tcpReader.ReadLine(); }
				catch (IOException) { line = null; }
				if (line == null) break;
				else if (string.IsNullOrWhiteSpace(line)) continue;

				var message = line.Trim();
				if (message.StartsWith("error ", StringComparison.Ordinal))
				{
					// we (hopefully) only need to lock here for the dequeue
					lock (lockObj)
					{
						if (!(status == TS3ClientStatus.Connected || status == TS3ClientStatus.Connecting)) break;

						var errorStatus = CommandDeserializer.GenerateErrorStatus(message);
						if (!errorStatus.Ok)
							requestQueue.Dequeue().SetAnswer(errorStatus);
						else
						{
							var peek = requestQueue.Any() ? requestQueue.Peek() : null;
							var response = CommandDeserializer.GenerateResponse(dataBuffer, peek?.AnswerType);
							dataBuffer = null;

							requestQueue.Dequeue().SetAnswer(errorStatus, response);
						}
					}
				}
				else if (message.StartsWith("notify", StringComparison.Ordinal))
				{
					var notify = CommandDeserializer.GenerateNotification(message);
					InvokeEvent(notify);
				}
				else
				{
					dataBuffer = line;
				}
			}
			status = TS3ClientStatus.Disconnected;
		}

		protected override IEnumerable<IResponse> SendCommand(string data, Type targetType) // Synchronous
		{
			using (WaitBlock wb = new WaitBlock(targetType))
			{
				lock (lockObj)
				{
					requestQueue.Enqueue(wb);
					SendRaw(data);
				}

				return wb.WaitForMessage();
			}
		}

		protected override void SendRaw(string data)
		{
			tcpWriter.WriteLine(data);
			tcpWriter.Flush();
		}
	}
}