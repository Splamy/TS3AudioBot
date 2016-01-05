using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TS3Query.Messages;
using TS3AudioBot.RessourceFactories;
using TS3AudioBot.Helper;

namespace TS3AudioBot
{
	class BobController : IDisposable
	{
		private const int CONNECT_TIMEOUT_MS = 10000;
		private const int CONNECT_TIMEOUT_INTERVAL_MS = 100;
		/// <summary>
		/// After TIMEOUT seconds, the bob disconnects.
		/// </summary>
		private const int BOB_TIMEOUT = 60;

		private BobControllerData data;
		private Task timerTask;
		private CancellationTokenSource cancellationTokenSource;
		private CancellationToken cancellationToken;
		private DateTime lastUpdate = DateTime.Now;

		private bool sending = false;
		private bool isRunning;
		private Queue<string> commandQueue;
		private readonly object lockObject = new object();
		private ClientData bobClient;

		private Dictionary<int, SubscriptionData> channelSubscriptions;

		public IQueryConnection QueryConnection { get; set; }

		private bool IsTimingOut
		{
			get { return timerTask != null && !timerTask.IsCompleted; }
		}

		public bool Sending
		{
			get { return sending; }
			set
			{
				sending = value;
				SendMessage("audio " + (value ? "on" : "off"));
			}
		}

		public BobController(BobControllerData data)
		{
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

		public async void OnRessourceStopped(bool restart)
		{
			if (!restart)
			{
				Sending = false;
				await StartEndTimer();
			}
		}

		public void Start()
		{
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
			if (IsTimingOut)
				cancellationTokenSource.Cancel();
		}

		public void Stop()
		{
			Log.Write(Log.Level.Info, "BC Stopping Bob");
			SendMessage("exit");
			isRunning = false;
			commandQueue.Clear();
			Log.Write(Log.Level.Debug, "BC bob is now officially dead");
			if (IsTimingOut)
				cancellationTokenSource.Cancel();
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
				bobClient = QueryConnection.GetClientById(e.ClientId);
				QueryConnection.OnClientConnect -= AwaitBobConnect;
				isRunning = true;
				Log.Write(Log.Level.Debug, "BC bob is now officially running");
				SendQueue();
			}
		}

		private async Task StartEndTimer()
		{
			HasUpdate();
			if (isRunning)
			{
				if (IsTimingOut)
				{
					cancellationTokenSource.Cancel();
					Log.Write(Log.Level.Debug, "BC cTS raised");
					await timerTask;
					Log.Write(Log.Level.Debug, "BC tT completed");
				}
				Log.Write(Log.Level.Debug, "BC start timeout");
				InternalStartEndTimer();
			}
		}

		private void InternalStartEndTimer()
		{
			cancellationTokenSource = new CancellationTokenSource();
			cancellationToken = cancellationTokenSource.Token;
			timerTask = Task.Run(async () =>
				{
					try
					{
						while (!cancellationToken.IsCancellationRequested)
						{
							double inactiveSeconds = (DateTime.Now - lastUpdate).TotalSeconds;
							if (inactiveSeconds > BOB_TIMEOUT)
							{
								Log.Write(Log.Level.Debug, "BC Timeout ran out...");
								Stop();
								break;
							}
							else
								await Task.Delay(TimeSpan.FromSeconds(BOB_TIMEOUT - inactiveSeconds), cancellationToken);
						}
					}
					catch (TaskCanceledException) { }
					catch (AggregateException) { }
				}, cancellationToken);
		}

		public void Dispose()
		{
			Stop();
			if (cancellationTokenSource != null)
			{
				cancellationTokenSource.Dispose();
				cancellationTokenSource = null;
			}
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