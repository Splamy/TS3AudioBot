namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using TS3AudioBot.Helper;
	using TS3Query.Messages;

	public sealed class BobController : IPlayerConnection
	{
		private static readonly TimeSpan BOB_TIMEOUT = TimeSpan.FromSeconds(60);

		private IQueryConnection queryConnection;
		private BobControllerData bobControllerData;
		private TickWorker timeout;
		private DateTime lastUpdate = DateTime.Now;
		private WaitEventBlock<MusicData> musicInfoWaiter;
		public MusicData CurrentMusicInfo { get; private set; }

		private bool awaitingConnect;
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
				return CurrentMusicInfo.Status == MusicStatus.Playing;
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
			if (queryConnection == null)
				throw new ArgumentNullException(nameof(queryConnection));

			timeout = TickPool.RegisterTick(TimeoutCheck, TimeSpan.FromMilliseconds(100), false);
			musicInfoWaiter = new WaitEventBlock<MusicData>();
			isRunning = false;
			awaitingConnect = false;
			this.bobControllerData = data;
			this.queryConnection = queryConnection;
			queryConnection.OnMessageReceived += GetResponse;
			queryConnection.OnClientConnect += OnBobConnect;
			queryConnection.OnClientDisconnect += OnBobDisconnnect;
			commandQueue = new Queue<string>();
			channelSubscriptions = new Dictionary<int, SubscriptionData>();
		}

		public void Initialize() { }

		#region SendMethods

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

		#endregion

		#region Response

		public void Callback(bool enable) => SendMessage("callback " + (enable ? "on" : "off"));

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
				case "status": musicData.Status = (MusicStatus)Enum.Parse(typeof(MusicStatus), result[1], true); break;
				case "title": musicData.Title = result[1]; break;
				case "volume": musicData.Volume = double.Parse(result[1], CultureInfo.InvariantCulture); break;
				default: Log.Write(Log.Level.Debug, "Unparsed key: {0}={1}", result[0], result[1]); break;
				}
			}
			return musicData;
		}

		#endregion

		#region Connect & Events

		internal void OnResourceStarted(object sender, PlayData playData)
		{
			BobStart();
			Sending = true;
			RestoreSubscriptions(playData.Invoker);
		}

		internal void OnResourceStopped(object sender, bool restart)
		{
			if (!restart)
			{
				Sending = false;
				BobStop();
			}
		}

		private void BobStart()
		{
			timeout.Active = false;
			if (!isRunning)
			{
				Callback(true);
				awaitingConnect = true;
				Log.Write(Log.Level.Debug, "BC now we are waiting for the bob");

				if (!Util.Execute(bobControllerData.startTSClient))
					Log.Write(Log.Level.Debug, "BC could not start bob");
			}
		}

		private void BobStop()
		{
			if (isRunning)
			{
				HasUpdate();
				Log.Write(Log.Level.Debug, "BC start timeout");
				timeout.Active = true;
			}
		}

		private void BobExit()
		{
			Log.Write(Log.Level.Info, "BC Stopping bob");
			SendMessage("exit");
		}

		private void OnBobConnect(object sender, ClientEnterView e)
		{
			if (!awaitingConnect) return;

			Log.Write(Log.Level.Debug, "BC user entered with GrId {0}", e.ServerGroups);
			if (e.ServerGroups.ToIntArray().Contains(bobControllerData.bobGroupId))
			{
				Log.Write(Log.Level.Debug, "BC user with correct UID found");
				bobClient = queryConnection.GetClientById(e.ClientId);
				isRunning = true;
				awaitingConnect = false;
				Log.Write(Log.Level.Debug, "BC bob is now officially running");
				SendQueue();
			}
		}

		private void OnBobDisconnnect(object sender, ClientLeftView e)
		{
			isRunning = false;
			commandQueue.Clear();
			timeout.Active = false;
			Log.Write(Log.Level.Debug, "BC bob is now officially dead");
		}

		public void HasUpdate()
		{
			lastUpdate = DateTime.Now;
		}

		private void TimeoutCheck()
		{
			if (lastUpdate + BOB_TIMEOUT < DateTime.Now)
			{
				Log.Write(Log.Level.Debug, "BC Timeout ran out...");
				BobExit();
			}
		}

		#endregion

		#region Subscriptions

		/// <summary>Adds a channel to the audio streaming list.</summary>
		/// <param name="channel">The id of the channel.</param>
		/// <param name="manual">Should be true if the command was invoked by a user,
		/// or false if the channel is added automatically by a play command.</param>
		public void WhisperChannelSubscribe(int channel, bool manual)
		{
			SendMessage("whisper channel add " + channel);
			SubscriptionData subscriptionData;
			if (!channelSubscriptions.TryGetValue(channel, out subscriptionData))
			{
				subscriptionData = new SubscriptionData { Id = channel, Manual = manual };
				channelSubscriptions.Add(channel, subscriptionData);
			}
			subscriptionData.Enabled = true;
			subscriptionData.Manual = subscriptionData.Manual || manual;
		}

		/// <summary>Removes a channel from the audio streaming list.</summary>
		/// <param name="channel">The id of the channel.</param>
		/// <param name="manual">Should be true if the command was invoked by a user,
		/// or false if the channel was removed automatically by an internal stop.</param>
		public void WhisperChannelUnsubscribe(int channel, bool manual)
		{
			SendMessage("whisper channel remove " + channel);
			SubscriptionData subscriptionData;
			if (!channelSubscriptions.TryGetValue(channel, out subscriptionData))
			{
				subscriptionData = new SubscriptionData { Id = channel, Manual = false };
				channelSubscriptions.Add(channel, subscriptionData);
			}
			if (manual)
			{
				subscriptionData.Manual = true;
				subscriptionData.Enabled = false;
			}
			else if (!subscriptionData.Manual)
			{
				subscriptionData.Enabled = false;
			}
		}

		public void WhisperClientSubscribe(int userId)
		{
			SendMessage("whisper client add " + userId);
		}

		public void WhisperClientUnsubscribe(int userId)
		{
			SendMessage("whisper client remove " + userId);
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

		private class SubscriptionData
		{
			public int Id { get; set; }
			public bool Enabled { get; set; }
			public bool Manual { get; set; }
		}

		#endregion

		public void Dispose()
		{
			if (musicInfoWaiter != null)
			{
				musicInfoWaiter.Dispose();
				musicInfoWaiter = null;
			}
			BobExit();
		}
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
		Off,
		Playing,
		Paused,
		Finished,
		Error,
	}

	public struct BobControllerData
	{
		[Info("ServerGroupID of the ServerBob")]
		public int bobGroupId;
		[Info("the path to a launch script or the teamspeak3 executable itself")]
		public string startTSClient;
	}
}