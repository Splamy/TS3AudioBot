using System;
using System.Collections.Generic;
using System.Linq;
using TS3Query.Messages;
using TS3AudioBot.RessourceFactories;
using TS3AudioBot.Helper;

namespace TS3AudioBot
{
	class BobController : IPlayerConnection
	{
		private const int CONNECT_TIMEOUT_MS = 10000;
		private const int CONNECT_TIMEOUT_INTERVAL_MS = 100;
		/// <summary>
		/// After TIMEOUT seconds, the bob disconnects.
		/// </summary>
		private const int BOB_TIMEOUT = 60;

		private BobControllerData data;
		private TickWorker timeout;
		private DateTime lastUpdate = DateTime.Now;

		private bool sending = false;
		private bool isRunning;
		private int volume = 100;
		private bool repeated = false;
		private bool pause = false;
		private Queue<string> commandQueue;
		private readonly object lockObject = new object();
		private ClientData bobClient;

		private Dictionary<int, SubscriptionData> channelSubscriptions;

		public IQueryConnection QueryConnection { get; set; }

		public bool Sending
		{
			get { return sending; }
			set
			{
				sending = value;
				SendMessage("audio " + (value ? "on" : "off"));
			}
		}

		public int Volume
		{
			get { return volume; }
			set
			{
				volume = value;
				SendMessage("music volume " + (value / 100.0));
			}
		}

		public int Position
		{
			get { return 0; } // TODO
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
				// There are also "music pause|unpause" but it unnecessary to send then
				// and the bob automatically pauses when it doesn't send.
				SendMessage("audio " + (value ? "on" : "off"));
			}
		}

		public BobController(BobControllerData data)
		{
			timeout = TickPool.RegisterTick(TimeoutCheck, 100, false);
			isRunning = false;
			this.data = data;
			commandQueue = new Queue<string>();
			channelSubscriptions = new Dictionary<int, SubscriptionData>();
		}

		private void SendMessage(string message)
		{
			lock (lockObject)
			{
				if (isRunning)
				{
					SendMessageRaw(message);
				}
				else
				{
					Log.Write(Log.Level.Debug, "BC Enqueing: {0}", message);
					commandQueue.Enqueue(message);
				}
			}
		}

		private void SendMessageRaw(string message)
		{
			//TODO lock here instead of sendmessage
			if (bobClient == null)
			{
				Log.Write(Log.Level.Debug, "BC bobClient is null! Message is lost: {0}", message);
				return;
			}

			Log.Write(Log.Level.Debug, "BC sending to bobC: {0}", message);
			QueryConnection.SendMessage(message, bobClient);
		}

		private void SendQueue()
		{
			if (!isRunning)
				throw new InvalidOperationException("The bob must run to send the commandQueue");

			while (commandQueue.Count > 0)
				SendMessageRaw(commandQueue.Dequeue());
		}

		public void HasUpdate()
		{
			lastUpdate = DateTime.Now;
		}

		public void OnRessourceStarted(AudioRessource ar)
		{
			Start();
			Sending = true;
			RestoreSubscriptions(ar.InvokingUser);
		}

		public void OnRessourceStopped(bool restart)
		{
			if (!restart)
			{
				Sending = false;
				StartEndTimer();
			}
		}

		public void Start()
		{
			timeout.Active = false;
			if (!isRunning)
			{
				// register callback to know immediatly when the bob connects
				Log.Write(Log.Level.Debug, "BC registering callback");
				QueryConnection.OnClientConnect += AwaitBobConnect;
				Log.Write(Log.Level.Debug, "BC now we are waiting for the bob");

				if (!Util.Execute(data.startTSClient))
				{
					Log.Write(Log.Level.Debug, "BC could not start bob");
					QueryConnection.OnClientConnect -= AwaitBobConnect;
					return;
				}
			}
		}

		public void Stop()
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

		public void AudioStart(string url)
		{
			SendMessage("music start " + url);
		}

		public void AudioStop()
		{
			SendMessage("music stop");
		}

		public int GetLength()
		{
			SendMessage("status music");
			//TODO get result
			return 0;
		}

		public bool IsPlaying()
		{
			SendMessage("status music");
			//TODO get result
			return false;
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
				bobClient = QueryConnection.GetClientById(e.ClientId);
				QueryConnection.OnClientConnect -= AwaitBobConnect;
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
				Stop();
				timeout.Active = false;
			}
		}

		public void Dispose()
		{
			Stop();
		}

		private class SubscriptionData
		{
			public int Id { get; set; }
			public bool Enabled { get; set; }
			public bool Manual { get; set; }
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