namespace TS3Query
{
	using System;
	using System.Collections.Concurrent;
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
	using TS3Query.Messages;
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

		public bool IsConnected { get; private set; }
		public string CurrentHost { get; private set; }
		public int CurrentPort { get; private set; }

		public IEventDispatcher EventDispatcher { get; private set; }

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
		private Thread readQueryThread;

		private readonly object lockObj = new object();
		private Queue<WaitBlock> requestQueue = new Queue<WaitBlock>();

		// STATIC LOOKUPS

		/// <summary>Maps the name of a notification to the class.</summary>
		private static Dictionary<string, Type> notifyLookup;
		/// <summary>Maps any QueryMessage class to all its fields
		/// the adding of QM's is lazy via the request method.</summary>
		private static Dictionary<Type, InitializerData> messageMap;
		/// <summary>Map of functions to deserialize from query values.</summary>
		private static Dictionary<Type, Func<string, Type, object>> convertMap;

		// CTORS

		static TS3QueryClient()
		{
			notifyLookup = new Dictionary<string, Type>();

			messageMap = new Dictionary<Type, InitializerData>();
			// get all classes deriving from Notification
			var derivedNtfy = from asm in AppDomain.CurrentDomain.GetAssemblies()
							  from type in asm.GetTypes()
							  where typeof(Notification).IsAssignableFrom(type)
							  select type;
			foreach (var eValue in derivedNtfy)
			{
				var ntfyAtt = (NotificationNameAttribute)eValue.GetCustomAttribute(typeof(NotificationNameAttribute), false);
				if (ntfyAtt == null) continue;

				notifyLookup.Add(ntfyAtt.Name, eValue);
			}

			convertMap = new Dictionary<Type, Func<string, Type, object>>();
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
		}

		public TS3QueryClient(EventDispatchType dispatcher)
		{
			tcpClient = new TcpClient();
			IsConnected = false;

			switch (dispatcher)
			{
			case EventDispatchType.None: EventDispatcher = new NoEventDispatcher(); break;
			case EventDispatchType.CurrentThread: throw new NotSupportedException(); //break;
			case EventDispatchType.Manual: EventDispatcher = new ManualEventDispatcher(); break;
			case EventDispatchType.AutoThreadPooled: throw new NotSupportedException(); //break;
			case EventDispatchType.NewThreadEach: throw new NotSupportedException(); //break;
			default: throw new NotSupportedException();
			}
		}

		// METHODS

		public void Connect() => Connect(defaultHost, defaultPort);
		public void Connect(string hostname) => Connect(hostname, defaultPort);
		public void Connect(string hostname, int port)
		{
			if (string.IsNullOrWhiteSpace(hostname)) throw new ArgumentNullException(nameof(hostname));
			if (port <= 0) throw new ArgumentOutOfRangeException(nameof(port));

			if (IsConnected)
				Quit();

			CurrentHost = hostname;
			CurrentPort = port;

			tcpClient.Connect(CurrentHost, CurrentPort);
			if (!tcpClient.Connected)
				throw new InvalidOperationException("Could not connect to the query server.");

			tcpStream = tcpClient.GetStream();
			tcpReader = new StreamReader(tcpStream);
			tcpWriter = new StreamWriter(tcpStream) { NewLine = "\n" };

			IsConnected = true;

			for (int i = 0; i < 3; i++)
				tcpReader.ReadLine();


			if (readQueryThread != null)
			{
				for (int i = 0; i < 100 && readQueryThread.IsAlive; i++)
					Thread.Sleep(1);
				if (readQueryThread.IsAlive)
				{
					readQueryThread.Abort();
					readQueryThread = null;
				}
			}
			readQueryThread = new Thread(ReadQueryLoop);
			readQueryThread.Name = "TS3Query MessageLoop";
			readQueryThread.Start();
		}

		#region QUERY METHODS

		public void Login(string username, string password) => Send("login",
			new Parameter("client_login_name", username),
			new Parameter("client_login_password", password));
		public void UseServer(int svrId) => Send("use",
			new Parameter("sid", svrId));
		public void ChangeName(string newName) => Send("clientupdate",
			new Parameter("client_nickname", newName));
		public void Quit()
		{
			tcpWriter.WriteLine("quit");
			tcpWriter.Flush();
			IsConnected = false;
		}
		public WhoAmI WhoAmI() => Send<WhoAmI>("whoami").FirstOrDefault();
		public void SendMessage(string message, ClientData client)
			=> SendMessage(MessageTarget.Private, client.Id, message);
		public void SendMessage(string message, ChannelData channel)
			=> SendMessage(MessageTarget.Channel, channel.Id, message);
		public void SendMessage(string message, ServerData server)
			=> SendMessage(MessageTarget.Server, server.Id, message);
		public void SendMessage(MessageTarget target, int id, string message) => Send("sendtextmessage",
			new Parameter("targetmode", (int)target),
			new Parameter("target", id),
			new Parameter("msg", message));
		public void SendGlobalMessage(string message) => Send("gm",
			new Parameter("msg", message));
		public void KickClientFromServer(ushort[] clientIds)
			=> KickClient(clientIds, RequestTarget.Server);
		public void KickClientFromChannel(ushort[] clientIds)
			=> KickClient(clientIds, RequestTarget.Channel);
		public void KickClient(ushort[] clientIds, RequestTarget target) => Send("clientkick",
			new Parameter("reasonid", (int)target),
			Binder.NewBind("clid", clientIds));
		public IEnumerable<ClientData> ClientList() => ClientList(0);
		public IEnumerable<ClientData> ClientList(ClientListOptions options) => Send<ClientData>("clientlist",
			NoParameter, options);

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

		private static void Subscribe<T>(ref EventHandler<T> handler, EventHandler<T> add) where T : Notification
		{
			handler = (EventHandler<T>)Delegate.Combine(handler, add);
		}
		private static void Unsubscribe<T>(ref EventHandler<T> handler, EventHandler<T> remove) where T : Notification
		{
			handler = (EventHandler<T>)Delegate.Remove(handler, remove);
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
				if (message.StartsWith("error "))
				{
					// we (hopefully) only need to lock here for the dequeue
					lock (lockObj)
					{
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
				else if (message.StartsWith("notify"))
				{
					var notify = GenerateNotification(message);
					InvokeEvent(notify);
				}
				else
				{
					dataBuffer = line;
				}
			}
			IsConnected = false;
		}

		private void InvokeEvent(Notification notification)
		{
			// TODO rework
			switch (notification.GetNotifyType())
			{
			case NotificationType.ChannelCreated: break;
			case NotificationType.ChannelDeleted: break;
			case NotificationType.ChannelChanged: break;
			case NotificationType.ChannelEdited: break;
			case NotificationType.ChannelMoved: break;
			case NotificationType.ChannelPasswordChanged: break;
			case NotificationType.ClientEnterView: EventDispatcher.Invoke(() => ClientEnterViewHandler(this, (ClientEnterView)notification)); break;
			case NotificationType.ClientLeftView: EventDispatcher.Invoke(() => ClientLeftViewHandler(this, (ClientLeftView)notification)); break;
			case NotificationType.ClientMoved: break;
			case NotificationType.ServerEdited: break;
			case NotificationType.TextMessage: EventDispatcher.Invoke(() => TextMessageReceivedHandler(this, (TextMessage)notification)); break;
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
				case "MSG": errorStatus.Message = responseParam.Value; break;
				case "FAILED_PERMID": errorStatus.MissingPermissionId = int.Parse(responseParam.Value); break;
				}
			}
			return errorStatus;
		}

		private static Notification GenerateNotification(string line)
		{
			int splitindex = line.IndexOf(' ');
			if (splitindex < 0) throw new ArgumentException("line couldn't be parsed");
			Type targetNotification;
			string notifyname = line.Substring(0, splitindex);
			if (notifyLookup.TryGetValue(notifyname, out targetNotification))
			{
				var notification = (Notification)Activator.CreateInstance(targetNotification);
				var incommingData = ParseKeyValueLine(line, true);
				FillQueryMessage(notification, incommingData);
				return notification;
			}
			else throw new NotSupportedException("No matching notification derivative");
		}

		private IEnumerable<Response> GenerateResponse(string line)
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
					return Enumerable.Empty<Response>();
				return messageList.Select(msg =>
				{
					var response = (Response)Activator.CreateInstance(peekResponse.AnswerType);
					FillQueryMessage(response, ParseKeyValueLine(msg, false));
					return response;
				});
			}
		}

		private static void FillQueryMessage(IQueryMessage qm, KVEnu kvpData)
		{
			var qmType = qm.GetType();
			var map = GetFieldMap(qmType);
			foreach (var kvp in kvpData)
			{
				var field = map.FieldMap[kvp.Key];
				object value = DeserializeValue(kvp.Value, field.FieldType);
				field.SetValue(qm, value);
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

		private static InitializerData GetFieldMap(Type type)
		{
			InitializerData map;
			if (!messageMap.TryGetValue(type, out map))
			{
				map = new InitializerData(type);
				foreach (var field in type.GetFields())
				{
					var serializerAtt = field.GetCustomAttribute<QuerySerializedAttribute>();
					if (serializerAtt == null) continue; // todo check is null ?

					map.FieldMap.Add(serializerAtt.Name, field);
				}
				messageMap.Add(type, map);
			}
			return map;
		}

		private static KVEnu ParseKeyValueLine(string line, bool ignoreFirst)
		{
			if (string.IsNullOrWhiteSpace(line))
				return Enumerable.Empty<KeyValuePair<string, string>>();
			IEnumerable<string> splitValues = line.Split(' ');
			if (ignoreFirst) splitValues = splitValues.Skip(1);
			return from part in splitValues
				   select part.Split(new[] { '=' }, 2) into keyValuePair
				   select new KeyValuePair<string, string>(keyValuePair[0], keyValuePair.Length > 1 ? keyValuePair[1] : null);
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
		public IEnumerable<T> Send<T>(string command) where T : Response
			=> Send<T>(command, NoParameter);

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command, params Parameter[] parameter) where T : Response
			=> Send<T>(command, parameter, NoOptions);

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command, Parameter[] parameter, Option options) where T : Response
			=> Send<T>(command, parameter, new[] { options });

		[DebuggerStepThrough]
		public IEnumerable<T> Send<T>(string command, Parameter[] parameter, params Option[] options) where T : Response
			=> SendInternal(command, parameter, options, typeof(T)).Cast<T>();

		protected IEnumerable<Response> SendInternal(string command, Parameter[] parameter, Option[] options, Type targetType)
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
				Quit();

				tcpReader.Dispose();
				tcpReader = null;

				tcpClient.Close();
				tcpClient = null;
			}

			if (EventDispatcher != null)
			{
				EventDispatcher.Dispose();
				EventDispatcher = null;
			}
		}
	}

	interface IEventDispatcher : IDisposable
	{
		EventDispatchType DispatcherType { get; }
		/// <summary>Do NOT call this method manually (Unless you know what you do).
		/// Invokes an Action, when the EventLoop receives a new packet.</summary>
		/// <param name="eventAction"></param>
		void Invoke(Action eventAction);
		/// <summary>Use this method to enter the read loop with the current Thread.</summary>
		void EnterEventLoop();
	}

	class ManualEventDispatcher : IEventDispatcher
	{
		public EventDispatchType DispatcherType => EventDispatchType.Manual;

		private ConcurrentQueue<Action> eventQueue = new ConcurrentQueue<Action>();
		private AutoResetEvent eventBlock = new AutoResetEvent(false);
		private bool run = true;

		public ManualEventDispatcher() { }

		public void Invoke(Action eventAction)
		{
			eventQueue.Enqueue(eventAction);
			eventBlock.Set();
		}

		public void EnterEventLoop()
		{
			while (run && eventBlock != null)
			{
				eventBlock.WaitOne();
				while (!eventQueue.IsEmpty)
				{
					Action callData;
					if (eventQueue.TryDequeue(out callData))
						callData.Invoke();
				}
			}
		}

		public void Dispose()
		{
			run = false;
			if (eventBlock != null)
			{
				eventBlock.Set();
				eventBlock.Dispose();
				eventBlock = null;
			}
		}
	}

	class NoEventDispatcher : IEventDispatcher
	{
		public EventDispatchType DispatcherType => EventDispatchType.None;
		public void EnterEventLoop() { throw new NotSupportedException(); }
		public void Invoke(Action eventAction) { }
		public void Dispose() { }
	}

	enum EventDispatchType
	{
		None,
		CurrentThread,
		Manual,
		AutoThreadPooled,
		NewThreadEach,
	}

	class InitializerData
	{
		public Type ActivationType { get; }
		public Dictionary<string, FieldInfo> FieldMap { get; }

		public InitializerData(Type type)
		{
			ActivationType = type;
			FieldMap = new Dictionary<string, FieldInfo>();
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
		private IEnumerable<Response> answer = null;
		private ErrorStatus errorStatus = null;
		public Type AnswerType { get; }

		public WaitBlock(Type answerType)
		{
			AnswerType = answerType;
		}

		public IEnumerable<Response> WaitForMessage()
		{
			waiter.WaitOne();
			if (!errorStatus.Ok)
				throw new QueryCommandException(errorStatus);
			return answer;
		}

		public void SetAnswer(ErrorStatus error, IEnumerable<Response> answer)
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
		public PrimitiveParameter(TimeSpan value) { QueryValue = value.TotalSeconds.ToString(); }

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

	public static class TS3QueryTools
	{
		public static string Escape(string stringToEscape)
		{
			StringBuilder strb = new StringBuilder(stringToEscape);
			strb = strb.Replace("\\", "\\\\"); // Backslash
			strb = strb.Replace("/", "\\/");   // Slash
			strb = strb.Replace(" ", "\\s");   // Whitespace
			strb = strb.Replace("|", "\\p");   // Pipe
			strb = strb.Replace("\f", "\\f");  // Formfeed
			strb = strb.Replace("\n", "\\n");  // Newline
			strb = strb.Replace("\r", "\\r");  // Carriage Return
			strb = strb.Replace("\t", "\\t");  // Horizontal Tab
			strb = strb.Replace("\v", "\\v");  // Vertical Tab
			return strb.ToString();
		}

		public static string Unescape(string stringToUnescape)
		{
			StringBuilder strb = new StringBuilder(stringToUnescape);
			strb = strb.Replace("\\v", "\v");  // Vertical Tab
			strb = strb.Replace("\\t", "\t");  // Horizontal Tab
			strb = strb.Replace("\\r", "\r");  // Carriage Return
			strb = strb.Replace("\\n", "\n");  // Newline
			strb = strb.Replace("\\f", "\f");  // Formfeed
			strb = strb.Replace("\\p", "|");   // Pipe
			strb = strb.Replace("\\s", " ");   // Whitespace
			strb = strb.Replace("\\/", "/");   // Slash
			strb = strb.Replace("\\\\", "\\"); // Backslash
			return strb.ToString();
		}
	}
}
