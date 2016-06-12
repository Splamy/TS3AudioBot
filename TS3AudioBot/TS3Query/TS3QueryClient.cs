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

namespace TS3Query
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Net.Sockets;
	using System.Reflection;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;
	using Messages;
	using KVEnu = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>>;

	class TS3QueryClient : IDisposable
	{
		// EVENTS
		private EventHandler<TextMessage> TextMessageReceivedHandler;
		public event EventHandler<TextMessage> OnTextMessageReceived
		{
			add { Subscribe(ref TextMessageReceivedHandler, value); }
			remove { Unsubscribe(ref TextMessageReceivedHandler, value); }
		}
		private EventHandler<ClientEnterView> ClientEnterViewHandler;
		public event EventHandler<ClientEnterView> OnClientEnterView
		{
			add { Subscribe(ref ClientEnterViewHandler, value); }
			remove { Unsubscribe(ref ClientEnterViewHandler, value); }
		}
		private EventHandler<ClientLeftView> ClientLeftViewHandler;
		public event EventHandler<ClientLeftView> OnClientLeftView
		{
			add { Subscribe(ref ClientLeftViewHandler, value); }
			remove { Unsubscribe(ref ClientLeftViewHandler, value); }
		}

		// SEMI-PUBLIC PROPERTIES

		public bool IsConnected => status == QueryClientStatus.Connected;
		public string CurrentHost { get; private set; }
		public int CurrentPort { get; private set; }

		// CONSTANTS

		private const string defaultHost = "127.0.0.1";
		private const short defaultPort = 10011;
		private static readonly Parameter[] NoParameter = new Parameter[0];
		private static readonly Option[] NoOptions = new Option[0];
		private static readonly Regex commandMatch = new Regex(@"[a-z0-9_]+", RegexOptions.Compiled);

		// PRIVATE STUFF

		private TcpClient tcpClient;
		private NetworkStream tcpStream;
		private StreamReader tcpReader;
		private StreamWriter tcpWriter;
		private IEventDispatcher EventDispatcher;

		enum QueryClientStatus
		{
			Disconnected,
			Connecting,
			Connected,
			Quitting,
		}
		private QueryClientStatus status;
		private bool isInQueue;
		private readonly object lockObj = new object();
		private Queue<WaitBlock> requestQueue = new Queue<WaitBlock>();

		// STATIC LOOKUPS

		/// <summary>Maps the name of a notification to the class.</summary>
		private static Dictionary<string, Type> notifyLookup;
		/// <summary>Map of functions to deserialize from query values.</summary>
		private static Dictionary<Type, Func<string, Type, object>> convertMap;

		// CTORS

		static TS3QueryClient()
		{
			// get all classes deriving from Notification
			var derivedNtfy = from asm in AppDomain.CurrentDomain.GetAssemblies()
							  from type in asm.GetTypes()
							  where type.IsInterface
							  where typeof(INotification).IsAssignableFrom(type)
							  let ntfyAtt = type.GetCustomAttribute(typeof(QueryNotificationAttribute), false)
							  where ntfyAtt != null
							  select new KeyValuePair<string, Type>(((QueryNotificationAttribute)ntfyAtt).Name, type);
			notifyLookup = derivedNtfy.ToDictionary(x => x.Key, x => x.Value);

			Helper.Init(ref convertMap);
			convertMap.Add(typeof(bool), (v, t) => v != "0");
			convertMap.Add(typeof(sbyte), (v, t) => sbyte.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(byte), (v, t) => byte.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(short), (v, t) => short.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(ushort), (v, t) => ushort.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(int), (v, t) => int.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(uint), (v, t) => uint.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(long), (v, t) => long.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(ulong), (v, t) => ulong.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(float), (v, t) => float.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(double), (v, t) => double.Parse(v, CultureInfo.InvariantCulture));
			convertMap.Add(typeof(string), (v, t) => TS3QueryTools.Unescape(v));
			convertMap.Add(typeof(TimeSpan), (v, t) => TimeSpan.FromSeconds(double.Parse(v, CultureInfo.InvariantCulture)));
			convertMap.Add(typeof(DateTime), (v, t) => PrimitiveParameter.unixTimeStart.AddSeconds(double.Parse(v, CultureInfo.InvariantCulture)));
		}

		public TS3QueryClient(EventDispatchType dispatcher)
		{
			status = QueryClientStatus.Disconnected;
			tcpClient = new TcpClient();
			isInQueue = false;

			switch (dispatcher)
			{
			case EventDispatchType.None: EventDispatcher = new NoEventDispatcher(); break;
			case EventDispatchType.CurrentThread: EventDispatcher = new CurrentThreadEventDisptcher(); break;
			case EventDispatchType.DoubleThread: EventDispatcher = new DoubleThreadEventDispatcher(); break;
			case EventDispatchType.AutoThreadPooled: throw new NotSupportedException(); //break;
			case EventDispatchType.NewThreadEach: throw new NotSupportedException(); //break;
			default: throw new NotSupportedException();
			}
		}

		// METHODS

		[DebuggerStepThrough]
		public void Connect() => Connect(defaultHost, defaultPort);
		[DebuggerStepThrough]
		public void Connect(string hostname) => Connect(hostname, defaultPort);
		public void Connect(string hostname, int port)
		{
			if (string.IsNullOrWhiteSpace(hostname)) throw new ArgumentNullException(nameof(hostname));
			if (port <= 0) throw new ArgumentOutOfRangeException(nameof(port));

			if (IsConnected)
				Close();

			status = QueryClientStatus.Connecting;
			CurrentHost = hostname;
			CurrentPort = port;

			try { tcpClient.Connect(CurrentHost, CurrentPort); }
			catch (SocketException ex) { throw new QueryCommandException(new ErrorStatus(), ex); }

			tcpStream = tcpClient.GetStream();
			tcpReader = new StreamReader(tcpStream);
			tcpWriter = new StreamWriter(tcpStream) { NewLine = "\n" };

			for (int i = 0; i < 3; i++)
				tcpReader.ReadLine();

			EventDispatcher.Init(ReadQueryLoop);
			status = QueryClientStatus.Connected;
		}

		public void Close()
		{
			lock (lockObj)
			{
				TextMessageReceivedHandler = null;
				ClientEnterViewHandler = null;
				ClientLeftViewHandler = null;

				status = QueryClientStatus.Quitting;
				tcpWriter.WriteLine("quit");
				tcpWriter.Flush();
			}
		}

		#region QUERY METHODS

		public void Login(string username, string password)
			=> Send("login",
			new Parameter("client_login_name", username),
			new Parameter("client_login_password", password));
		public void UseServer(int svrId)
			=> Send("use",
			new Parameter("sid", svrId));
		public void ChangeName(string newName)
			=> Send("clientupdate",
			new Parameter("client_nickname", newName));
		public void ChangeDescription(string newDescription, ClientData client)
			=> Send("clientdbedit",
			new Parameter("cldbid", client.DatabaseId),
			new Parameter("client_description", newDescription));
		public void Quit() => Close();
		public WhoAmI WhoAmI()
			=> Send<WhoAmI>("whoami").FirstOrDefault();
		public void SendMessage(string message, ClientData client)
			=> SendMessage(MessageTarget.Private, client.ClientId, message);
		public void SendMessage(string message, ChannelData channel)
			=> SendMessage(MessageTarget.Channel, channel.Id, message);
		public void SendMessage(string message, ServerData server)
			=> SendMessage(MessageTarget.Server, server.VirtualServerId, message);
		public void SendMessage(MessageTarget target, int id, string message)
			=> Send("sendtextmessage",
			new Parameter("targetmode", (int)target),
			new Parameter("target", id),
			new Parameter("msg", message));
		public void SendGlobalMessage(string message)
			=> Send("gm",
			new Parameter("msg", message));
		public void KickClientFromServer(ushort[] clientIds)
			=> KickClient(clientIds, RequestTarget.Server);
		public void KickClientFromChannel(ushort[] clientIds)
			=> KickClient(clientIds, RequestTarget.Channel);
		public void KickClient(ushort[] clientIds, RequestTarget target)
			=> Send("clientkick",
			new Parameter("reasonid", (int)target),
			Binder.NewBind("clid", clientIds));
		public IEnumerable<ClientData> ClientList()
			=> ClientList(0);
		public IEnumerable<ClientData> ClientList(ClientListOptions options) => Send<ClientData>("clientlist",
			NoParameter, options);
		public IEnumerable<ClientServerGroup> ServerGroupsOfClientDbId(ClientData client)
			=> ServerGroupsOfClientDbId(client.DatabaseId);
		public IEnumerable<ClientServerGroup> ServerGroupsOfClientDbId(ulong clDbId)
			=> Send<ClientServerGroup>("servergroupsbyclientid", new Parameter("cldbid", clDbId));
		public ClientDbData ClientDbInfo(ClientData client)
			=> ClientDbInfo(client.DatabaseId);
		public ClientDbData ClientDbInfo(ulong clDbId)
			=> Send<ClientDbData>("clientdbinfo", new Parameter("cldbid", clDbId)).FirstOrDefault();


		#endregion

		// TODO automate
		public void RegisterNotification(MessageTarget target, int channel) => RegisterNotification(target.GetQueryString(), channel);
		public void RegisterNotification(RequestTarget target, int channel) => RegisterNotification(target.GetQueryString(), channel);
		private void RegisterNotification(string target, int channel)
		{
			var ev = new Parameter("event", target.ToString().ToLowerInvariant());
			if (target == "channel")
				Send("servernotifyregister", ev, new Parameter("id", channel));
			else
				Send("servernotifyregister", ev);
		}

		private static void Subscribe<T>(ref EventHandler<T> handler, EventHandler<T> add) where T : INotification
		{
			handler = (EventHandler<T>)Delegate.Combine(handler, add);
		}
		private static void Unsubscribe<T>(ref EventHandler<T> handler, EventHandler<T> remove) where T : INotification
		{
			handler = (EventHandler<T>)Delegate.Remove(handler, remove);
		}

		/// <summary>Use this method to start the event dispatcher.
		/// Please keep in mind that this call might be blocking or non-blocking depending on the dispatch-method.
		/// <see cref="EventDispatchType.CurrentThread"/> and <see cref="EventDispatchType.DoubleThread"/> will enter a loop and block the calling thread.
		/// Any other method will start special subroutines and return to the caller.</summary>
		public void EnterEventLoop()
		{
			if (!isInQueue)
			{
				isInQueue = true;
				EventDispatcher.EnterEventLoop();
			}
			else throw new InvalidOperationException("EventLoop can only be run once until disposed.");
		}

		private void ReadQueryLoop()
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
						if (!(status == QueryClientStatus.Connected || status == QueryClientStatus.Connecting)) break;

						var errorStatus = GenerateErrorStatus(message);
						if (!errorStatus.Ok)
							requestQueue.Dequeue().SetAnswer(errorStatus);
						else
						{
							var response = GenerateResponse(dataBuffer);
							dataBuffer = null;

							requestQueue.Dequeue().SetAnswer(errorStatus, response);
						}
					}
				}
				else if (message.StartsWith("notify", StringComparison.Ordinal))
				{
					var notify = GenerateNotification(message);
					InvokeEvent(notify);
				}
				else
				{
					dataBuffer = line;
				}
			}
			status = QueryClientStatus.Disconnected;
		}

		private void InvokeEvent(INotification notification)
		{
			// TODO rework
			switch (notification.NotifyType)
			{
			case NotificationType.ChannelCreated: break;
			case NotificationType.ChannelDeleted: break;
			case NotificationType.ChannelChanged: break;
			case NotificationType.ChannelEdited: break;
			case NotificationType.ChannelMoved: break;
			case NotificationType.ChannelPasswordChanged: break;
			case NotificationType.ClientEnterView: EventDispatcher.Invoke(() => ClientEnterViewHandler?.Invoke(this, (ClientEnterView)notification)); break;
			case NotificationType.ClientLeftView: EventDispatcher.Invoke(() => ClientLeftViewHandler?.Invoke(this, (ClientLeftView)notification)); break;
			case NotificationType.ClientMoved: break;
			case NotificationType.ServerEdited: break;
			case NotificationType.TextMessage: EventDispatcher.Invoke(() => TextMessageReceivedHandler?.Invoke(this, (TextMessage)notification)); break;
			case NotificationType.TokenUsed: break;
			default: throw new InvalidOperationException();
			}
		}

		private static ErrorStatus GenerateErrorStatus(string line)
		{
			var kvpList = ParseKeyValueLine(line, true);
			var errorStatus = new ErrorStatus();
			foreach (var responseParam in kvpList)
			{
				switch (responseParam.Key.ToUpperInvariant())
				{
				case "ID": errorStatus.Id = int.Parse(responseParam.Value); break;
				case "MSG": errorStatus.Message = TS3QueryTools.Unescape(responseParam.Value); break;
				case "FAILED_PERMID": errorStatus.MissingPermissionId = int.Parse(responseParam.Value); break;
				}
			}
			return errorStatus;
		}

		private static INotification GenerateNotification(string line)
		{
			int splitindex = line.IndexOf(' ');
			if (splitindex < 0) throw new ArgumentException("line couldn't be parsed");
			Type targetNotification;
			string notifyname = line.Substring(0, splitindex);
			if (notifyLookup.TryGetValue(notifyname, out targetNotification))
			{
				var notification = Generator.ActivateNotification(targetNotification);
				var incommingData = ParseKeyValueLine(line, true);
				FillQueryMessage(targetNotification, notification, incommingData);
				return notification;
			}
			else throw new NotSupportedException("No matching notification derivative");
		}

		private IEnumerable<IResponse> GenerateResponse(string line)
		{
			if (!requestQueue.Any())
				throw new InvalidOperationException();

			var peekResponse = requestQueue.Peek();

			var messageList = line?.Split('|');
			if (peekResponse.AnswerType == null)
			{
				if (string.IsNullOrWhiteSpace(line))
					return Enumerable.Empty<ResponseDictionary>();
				return messageList.Select(msg => new ResponseDictionary(ParseKeyValueLineDict(msg, false)));
			}
			else
			{
				if (string.IsNullOrWhiteSpace(line))
					return Enumerable.Empty<IResponse>();
				return messageList.Select(msg =>
				{
					var response = Generator.ActivateResponse(peekResponse.AnswerType);
					FillQueryMessage(peekResponse.AnswerType, response, ParseKeyValueLine(msg, false));
					return response;
				});
			}
		}

		private static void FillQueryMessage(Type baseType, IQueryMessage qm, KVEnu kvpData)
		{
			var map = Generator.GetAccessMap(baseType);
			foreach (var kvp in kvpData)
			{
				PropertyInfo prop;
				if (!map.TryGetValue(kvp.Key, out prop))
				{
					Debug.Write($"Missing Parameter '{kvp.Key}' in '{qm}'");
					continue;
				}
				object value = DeserializeValue(kvp.Value, prop.PropertyType);
				prop.SetValue(qm, value);
			}
		}

		private static object DeserializeValue(string data, Type dataType)
		{
			Func<string, Type, object> converter;
			if (convertMap.TryGetValue(dataType, out converter))
				return converter(data, dataType);
			else if (dataType.IsEnum)
				return Enum.ToObject(dataType, Convert.ChangeType(data, dataType.GetEnumUnderlyingType()));
			else
				throw new NotSupportedException();
		}

		private static KVEnu ParseKeyValueLine(string line, bool ignoreFirst)
		{
			if (string.IsNullOrWhiteSpace(line))
				return Enumerable.Empty<KeyValuePair<string, string>>();
			IEnumerable<string> splitValues = line.Split(' ');
			if (ignoreFirst) splitValues = splitValues.Skip(1);
			return from part in splitValues
				   select part.Split(new[] { '=' }, 2) into keyValuePair
				   select new KeyValuePair<string, string>(keyValuePair[0], keyValuePair.Length > 1 ? keyValuePair[1] : string.Empty);
		}
		private static IDictionary<string, string> ParseKeyValueLineDict(string line, bool ignoreFirst)
			=> ParseKeyValueLineDict(ParseKeyValueLine(line, ignoreFirst));
		private static IDictionary<string, string> ParseKeyValueLineDict(KVEnu data)
			=> data.ToDictionary(pair => pair.Key, pair => pair.Value);

		#region NETWORK METHODS

		[DebuggerStepThrough]
		public IEnumerable<ResponseDictionary> Send(string command)
			=> Send(command, NoParameter);

		[DebuggerStepThrough]
		public IEnumerable<ResponseDictionary> Send(string command, params Parameter[] parameter)
			=> Send(command, parameter, NoOptions);

		[DebuggerStepThrough]
		public IEnumerable<ResponseDictionary> Send(string command, Parameter[] parameter, Option options)
			=> Send(command, parameter, new[] { options });

		[DebuggerStepThrough]
		public IEnumerable<ResponseDictionary> Send(string command, Parameter[] parameter, params Option[] options)
			=> SendInternal(command, parameter, options, null).Cast<ResponseDictionary>();

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command) where T : IResponse
			=> Send<T>(command, NoParameter);

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command, params Parameter[] parameter) where T : IResponse
			=> Send<T>(command, parameter, NoOptions);

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command, Parameter[] parameter, Option options) where T : IResponse
			=> Send<T>(command, parameter, new[] { options });

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command, Parameter[] parameter, params Option[] options) where T : IResponse
			=> SendInternal(command, parameter, options, typeof(T)).Cast<T>();

		protected IEnumerable<IResponse> SendInternal(string command, Parameter[] parameter, Option[] options, Type targetType)
		{
			if (string.IsNullOrWhiteSpace(command))
				throw new ArgumentNullException(nameof(command));
			if (!commandMatch.IsMatch(command))
				throw new ArgumentException("Invalid command characters", nameof(command));

			StringBuilder strb = new StringBuilder(TS3QueryTools.Escape(command));

			foreach (var param in parameter)
				strb.Append(' ').Append(param.QueryString);

			foreach (var option in options)
				strb.Append(option.Value);

			string finalCommand = strb.ToString();
			using (WaitBlock wb = new WaitBlock(targetType))
			{
				lock (lockObj)
				{
					requestQueue.Enqueue(wb);

					tcpWriter.WriteLine(finalCommand);
					tcpWriter.Flush();
				}

				return wb.WaitForMessage();
			}
		}

		#endregion

		public void Dispose()
		{
			if (IsConnected)
			{
				TextMessageReceivedHandler = null;
				ClientEnterViewHandler = null;
				ClientLeftViewHandler = null;

				Quit();

				lock (lockObj)
				{
					tcpWriter.Dispose();
					tcpWriter = null;

					tcpReader.Dispose();
					tcpReader = null;

					tcpClient.Close();
					tcpClient = null;
				}
			}

			if (EventDispatcher != null)
			{
				EventDispatcher.Dispose();
				EventDispatcher = null;
			}
		}
	}

	public class ErrorStatus
	{
		// id
		public int Id { get; set; }
		// msg
		public string Message { get; set; }
		// failed_permid
		public int MissingPermissionId { get; set; } = -1;

		public bool Ok => Id == 0 && Message == "ok";

		public string ErrorFormat() => $"{Id}: the command failed to execute: {Message} (missing permission:{MissingPermissionId})";
	}

	class WaitBlock : IDisposable
	{
		private AutoResetEvent waiter = new AutoResetEvent(false);
		private IEnumerable<IResponse> answer = null;
		private ErrorStatus errorStatus = null;
		public Type AnswerType { get; }

		public WaitBlock(Type answerType)
		{
			AnswerType = answerType;
		}

		public IEnumerable<IResponse> WaitForMessage()
		{
			waiter.WaitOne();
			if (!errorStatus.Ok)
				throw new QueryCommandException(errorStatus);
			return answer;
		}

		public void SetAnswer(ErrorStatus error, IEnumerable<IResponse> answer)
		{
			this.answer = answer;
			SetAnswer(error);
		}

		public void SetAnswer(ErrorStatus error)
		{
			if (error == null)
				throw new ArgumentNullException(nameof(error));
			errorStatus = error;
			waiter.Set();
		}

		public void Dispose()
		{
			if (waiter != null)
			{
				waiter.Set();
				waiter.Dispose();
				waiter = null;
			}
		}
	}

	[Serializable]
	public class QueryCommandException : Exception
	{
		public ErrorStatus ErrorStatus { get; private set; }

		internal QueryCommandException(ErrorStatus message) : base(message.ErrorFormat()) { ErrorStatus = message; }
		internal QueryCommandException(ErrorStatus message, Exception inner) : base(message.ErrorFormat(), inner) { ErrorStatus = message; }
	}

	public class Parameter
	{
		public string Key { get; protected set; }
		public string Value { get; protected set; }
		public virtual string QueryString => string.Concat(Key, "=", Value);

		protected Parameter() { }

		public Parameter(string name, IParameterConverter rawValue)
		{
			Key = name;
			Value = rawValue.QueryValue;
		}

		public Parameter(string name, PrimitiveParameter value) : this(name, (IParameterConverter)value) { }
	}

	public class Option
	{
		public string Value { get; protected set; }

		public Option(string name) { Value = string.Concat(" -", name); }
		public Option(Enum values) { Value = string.Join(" -", values.GetFlags().Select(enu => Enum.GetName(typeof(Enum), enu))); }

		public static implicit operator Option(string value) => new Option(value);
		public static implicit operator Option(Enum value) => new Option(value);
	}

	public interface IParameterConverter
	{
		string QueryValue { get; }
	}

	public class PrimitiveParameter : IParameterConverter
	{
		public string QueryValue { get; }
		public static readonly DateTime unixTimeStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

		public PrimitiveParameter(bool value) { QueryValue = (value ? "1" : "0"); }
		public PrimitiveParameter(sbyte value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(byte value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(short value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(ushort value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(int value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(uint value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(long value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(ulong value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(float value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(double value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(string value) { QueryValue = TS3QueryTools.Escape(value); }
		public PrimitiveParameter(TimeSpan value) { QueryValue = value.TotalSeconds.ToString("F0"); }
		public PrimitiveParameter(DateTime value) { QueryValue = (value - unixTimeStart).TotalSeconds.ToString("F0"); }

		public static implicit operator PrimitiveParameter(bool value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(sbyte value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(byte value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(short value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(ushort value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(int value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(uint value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(long value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(ulong value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(float value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(double value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(string value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(TimeSpan value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(DateTime value) => new PrimitiveParameter(value);
	}

	public class Binder : Parameter
	{
		private static Dictionary<Type, ConstructorInfo> constrBuffer = new Dictionary<Type, ConstructorInfo>();
		private static ConstructorInfo GetValueCtor(Type t)
		{
			ConstructorInfo ci;
			if (!constrBuffer.TryGetValue(t, out ci))
			{
				var ctor = typeof(PrimitiveParameter).GetConstructors().Where(c => c.GetParameters().First().ParameterType == t).FirstOrDefault();
				if (ctor == null)
					throw new InvalidCastException();
				ci = ctor;
				constrBuffer.Add(t, ci);
			}
			return ci;
		}
		private List<string> buildList = new List<string>();
		public override string QueryString => string.Join(" ", buildList);

		protected Binder() { }

		public static Binder NewBind<T>(string key, IEnumerable<T> parameter) => new Binder().Bind<T>(key, parameter);
		public Binder Bind<T>(string key, IEnumerable<T> parameter)
		{
			var ctor = GetValueCtor(typeof(T));
			var values = parameter.Select(val => (PrimitiveParameter)ctor.Invoke(new object[] { val }));
			var result = string.Join("|", values.Select(v => new Parameter(key, v).QueryString));
			buildList.Add(result);
			return this;
		}

		public static Binder NewBind(string key, IEnumerable<Parameter> parameter) => new Binder().Bind(key, parameter);
		public Binder Bind<T>(string key, IEnumerable<Parameter> parameter)
		{
			throw new NotImplementedException();
			//buildList.Add(result);
			//return this;
		}
	}
}
