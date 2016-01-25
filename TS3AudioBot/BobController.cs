namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using TS3AudioBot.Helper;
	using TS3Query.Messages;

	public class BobController : IPlayerConnection
	{
		/// <summary>After TIMEOUT seconds, the bob disconnects.</summary>
		private const int BOB_TIMEOUT = 60;

		private IQueryConnection queryConnection;
		private BobControllerData data;
		private TickWorker timeout;
		private DateTime lastUpdate = DateTime.Now;
		private WaitEventBlock<MusicData> musicInfoWaiter;
		public MusicData CurrentMusicInfo { get; private set; }

		private bool isRunning;
		private Queue<string> commandQueue;
		private readonly object lockObject = new object();
		private ClientData bobClient;

		private Dictionary<int, SubscriptionData> channelSubscriptions;

		private bool sending = false;
		public bool Sending
		{
			get { return sending; }
			set
			{
				sending = value;
				SendMessage("audio " + (value ? "on" : "off"));
			}
		}

		public bool Callback { set { SendMessage("callback " + (value ? "on" : "off")); } }

		#region IPlayerConnection

		private int volume = -1;
		private bool repeated = false;
		private bool pause = false;

		public bool SupportsEndCallback => true;
		public event EventHandler OnSongEnd;

		public int Volume
		{
			get { return volume; }
			set
			{
				volume = value;
				SendMessage("music volume " + (value / 100d));
			}
		}

		public int Position
		{
			get
			{
				SendMessage("status music");
				musicInfoWaiter.Wait();
				return (int)CurrentMusicInfo.Position;
			}
			set
			{
				SendMessage("music seek " + value);
			}
		}

		public bool Repeated
		{
			get { return repeated; }
			set
			{
				repeated = value;
				SendMessage("music loop " + (value ? "on" : "off"));
			}
		}

		public bool Pause
		{
			get { return pause; }
			set
			{
				pause = value;
				// The commands "music pause|unpause" are available, but redundant
				// since the bob automatically pauses when the audio output is off.
				SendMessage("audio " + (value ? "on" : "off"));
			}
		}

		public int Length
		{
			get
			{
				SendMessage("status music");
				musicInfoWaiter.Wait();
				return (int)CurrentMusicInfo.Length;
			}
		}

		public bool IsPlaying
		{
			get
			{
				SendMessage("status music");
				musicInfoWaiter.Wait();
				return CurrentMusicInfo.Status == MusicStatus.playing;
			}
		}


		public void AudioStart(string url)
		{
			SendMessage("music start " + url);
		}

		public void AudioStop()
		{
			SendMessage("music stop");
		}

		#endregion

		public BobController(BobControllerData data, IQueryConnection queryConnection)
		{
			timeout = TickPool.RegisterTick(TimeoutCheck, 100, false);
			musicInfoWaiter = new WaitEventBlock<MusicData>();
			isRunning = false;
			this.data = data;
			this.queryConnection = queryConnection;
			queryConnection.OnMessageReceived += GetResponse;
			commandQueue = new Queue<string>();
			channelSubscriptions = new Dictionary<int, SubscriptionData>();
		}

		public void Initialize() { }

		public void SendMessage(string message)
		{
			if (isRunning)
			{
				lock (lockObject)
					SendMessageRaw(message);
			}
			else
			{
				Log.Write(Log.Level.Debug, "BC Enqueing: {0}", message);
				commandQueue.Enqueue(message);
			}
		}

		private void SendMessageRaw(string message)
		{
			if (bobClient == null)
			{
				Log.Write(Log.Level.Debug, "BC bobClient is null! Message is lost: {0}", message);
				return;
			}

			Log.Write(Log.Level.Debug, "BC sending to bobC: {0}", message);
			queryConnection.SendMessage(message, bobClient);
		}

		private void SendQueue()
		{
			if (!isRunning)
				throw new InvalidOperationException("The bob must run to send the commandQueue");

			lock (lockObject)
				while (commandQueue.Count > 0)
					SendMessageRaw(commandQueue.Dequeue());
		}

		internal void GetResponse(object sender, TextMessage message)
		{
			if (bobClient == null)
				return;
			if (message.InvokerId != bobClient.Id)
				return;

			ParseData(TextUtil.RemoveUrlBB(message.Message));
		}

		private void ParseData(string input)
		{
			var splits = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Split(new[] { ' ' }, 2));
			var typeKVP = splits.FirstOrDefault();
			if (typeKVP == null)
				throw new InvalidOperationException("Empty response");
			splits = splits.Skip(1);
			switch (typeKVP[0])
			{
			case "error": Log.Write(Log.Level.Warning, "Erroneous answer: {0}", typeKVP[1]); break;
			case "answer":
				switch (typeKVP[1])
				{
				case "music": musicInfoWaiter.Notify(CurrentMusicInfo = ParseMusicData(splits)); break;
				case "audio": break;
				case "end_event": break;
				default: throw new NotSupportedException("Answer not recognized");
				}
				break;
			case "callback":
				switch (typeKVP[1])
				{
				// Error during decoding (can be ignored)
				case "musicdecodeerror": break;
				// Fatal error, song cannot be started/continued
				case "musicreaderror":
				// song has finished
				case "musicfinished":
					OnSongEnd?.Invoke(this, new EventArgs());
					break;
				default: throw new NotSupportedException("Callback not recognized");
				}
				break;
			case "pong": Log.Write(Log.Level.Debug, "Alrighty then!"); break;
			default: throw new NotSupportedException("Response not recognized");
			}
		}

		public void HasUpdate()
		{
			lastUpdate = DateTime.Now;
		}

		internal void OnResourceStarted(PlayData playData)
		{
			BobStart();
			Sending = true;
			RestoreSubscriptions(playData.Invoker);
		}

		internal void OnResourceStopped(bool restart)
		{
			if (!restart)
			{
				Sending = false;
				StartEndTimer();
			}
		}

		internal void BobStart()
		{
			timeout.Active = false;
			if (!isRunning)
			{
				Callback = true;
				// register callback to know immediatly when the bob connects
				Log.Write(Log.Level.Debug, "BC registering callback");
				queryConnection.OnClientConnect += AwaitBobConnect;
				Log.Write(Log.Level.Debug, "BC now we are waiting for the bob");

				if (!Util.Execute(data.startTSClient))
				{
					Log.Write(Log.Level.Debug, "BC could not start bob");
					queryConnection.OnClientConnect -= AwaitBobConnect;
					return;
				}
			}
		}

		internal void BobStop()
		{
			Log.Write(Log.Level.Info, "BC Stopping bob");
			SendMessage("exit");
			isRunning = false;
			commandQueue.Clear();
			Log.Write(Log.Level.Debug, "BC bob is now officially dead");
		}

		/// <summary>Adds a channel to the audio streaming list.</summary>
		/// <param name="channel">The id of the channel.</param>
		/// <param name="manual">Should be true if the command was invoked by a user,
		/// or false if the channel is added automatically by a play command.</param>
		public void WhisperChannelSubscribe(int channel, bool manual)
		{
			SendMessage("whisper channel add " + channel);
			SubscriptionData data;
			if (!channelSubscriptions.TryGetValue(channel, out data))
			{
				data = new SubscriptionData { Id = channel, Manual = manual };
				channelSubscriptions.Add(channel, data);
			}
			data.Enabled = true;
			data.Manual = data.Manual || manual;
		}

		/// <summary>Removes a channel from the audio streaming list.</summary>
		/// <param name="channel">The id of the channel.</param>
		/// <param name="manual">Should be true if the command was invoked by a user,
		/// or false if the channel was removed automatically by an internal stop.</param>
		public void WhisperChannelUnsubscribe(int channel, bool manual)
		{
			SendMessage("whisper channel remove " + channel);
			SubscriptionData data;
			if (!channelSubscriptions.TryGetValue(channel, out data))
			{
				data = new SubscriptionData { Id = channel, Manual = false };
				channelSubscriptions.Add(channel, data);
			}
			if (manual)
			{
				data.Manual = true;
				data.Enabled = false;
			}
			else if (!data.Manual)
			{
				data.Enabled = false;
			}
		}

		public void WhisperClientSubscribe(int userID)
		{
			SendMessage("whisper client add " + userID);
		}

		public void WhisperClientUnsubscribe(int userID)
		{
			SendMessage("whisper client remove " + userID);
		}

		private void RestoreSubscriptions(ClientData invokingUser)
		{
			WhisperChannelSubscribe(invokingUser.ChannelId, false);
			foreach (var data in channelSubscriptions)
			{
				if (data.Value.Enabled)
				{
					if (data.Value.Manual)
						WhisperChannelSubscribe(data.Value.Id, false);
					else if (!data.Value.Manual && invokingUser.ChannelId != data.Value.Id)
						WhisperChannelUnsubscribe(data.Value.Id, false);
				}
			}
		}

		private void AwaitBobConnect(object sender, ClientEnterView e)
		{
			Log.Write(Log.Level.Debug, "BC user entered with GrId {0}", e.ServerGroups);
			if (e.ServerGroups.ToIntArray().Contains(data.bobGroupId))
			{
				Log.Write(Log.Level.Debug, "BC user with correct UID found");
				bobClient = queryConnection.GetClientById(e.ClientId);
				queryConnection.OnClientConnect -= AwaitBobConnect;
				isRunning = true;
				Log.Write(Log.Level.Debug, "BC bob is now officially running");
				SendQueue();
			}
		}

		private void StartEndTimer()
		{
			HasUpdate();
			if (isRunning)
			{
				Log.Write(Log.Level.Debug, "BC start timeout");
				timeout.Active = true;
			}
		}

		private void TimeoutCheck()
		{
			double inactiveSeconds = (DateTime.Now - lastUpdate).TotalSeconds;
			if (inactiveSeconds > BOB_TIMEOUT)
			{
				Log.Write(Log.Level.Debug, "BC Timeout ran out...");
				BobStop();
				timeout.Active = false;
			}
		}

		public void Dispose()
		{
			BobStop();
		}

		private static MusicData ParseMusicData(IEnumerable<string[]> input)
		{
			var musicData = new MusicData();
			foreach (var result in input)
			{
				switch (result[0])
				{
				case "address": musicData.Address = result[1]; break;
				case "length": musicData.Length = double.Parse(result[1], CultureInfo.InvariantCulture); break;
				case "loop": musicData.Loop = result[1] != "off"; break;
				case "position": musicData.Position = double.Parse(result[1], CultureInfo.InvariantCulture); break;
				case "status": musicData.Status = (MusicStatus)Enum.Parse(typeof(MusicStatus), result[1]); break;
				case "title": musicData.Title = result[1]; break;
				case "volume": musicData.Volume = double.Parse(result[1], CultureInfo.InvariantCulture); break;
				default: Log.Write(Log.Level.Debug, "Unparsed key: {0}={1}", result[0], result[1]); break;
				}
			}
			return musicData;
		}

		private class SubscriptionData
		{
			public int Id { get; set; }
			public bool Enabled { get; set; }
			public bool Manual { get; set; }
		}

		public class MusicData
		{
			public MusicStatus Status { get; set; }
			public double Length { get; set; }
			public double Position { get; set; }
			public string Title { get; set; }
			public string Address { get; set; }
			public bool Loop { get; set; }
			public double Volume { get; set; }
		}

		public enum MusicStatus
		{
			off,
			playing,
			paused,
			finished,
			error,
		}
	}

	public struct BobControllerData
	{
		[Info("ServerGroupID of the ServerBob")]
		public int bobGroupId;
		[Info("the path to a launch script or the teamspeak3 executable itself")]
		public string startTSClient;
	}
}