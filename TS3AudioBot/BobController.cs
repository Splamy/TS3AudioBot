using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using TeamSpeak3QueryApi.Net.Specialized.Responses;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;

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

		private HashSet<int> whisperChannel;
		private Queue<string> commandQueue;
		private readonly object lockObject = new object();
		private GetClientsInfo bobClient;

		public QueryConnection QueryConnection { get; set; }

		public bool IsRunning { get; private set; }

		public bool IsTimingOut
		{
			get { return timerTask != null && !timerTask.IsCompleted; }
		}

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

		public BobController(BobControllerData data)
		{
			IsRunning = false;
			this.data = data;
			commandQueue = new Queue<string>();
			whisperChannel = new HashSet<int>();
		}

		private void SendMessage(string message)
		{
			lock (lockObject)
			{
				if (IsRunning)
				{
					if (bobClient == null)
					{
						Log.Write(Log.Level.Debug, "BC bobClient is null! Message is lost: {0}", message);
						return;
					}
					var sendTask = SendMessageRaw(message);
				}
				else
				{
					Log.Write(Log.Level.Debug, "BC Enqueing: {0}", message);
					commandQueue.Enqueue(message);
				}
			}
		}

		private async Task SendMessageRaw(string message)
		{
			Log.Write(Log.Level.Debug, "BC sending to bobC: {0}", message);
			await QueryConnection.TSClient.SendMessage(message, bobClient);
		}

		private async Task SendQueue()
		{
			if (!IsRunning)
				throw new InvalidOperationException("The bob must run to send the commandQueue");

			while (commandQueue.Count > 0)
				await SendMessageRaw(commandQueue.Dequeue());
		}

		public void HasUpdate()
		{
			lastUpdate = DateTime.Now;
		}

		public void Start()
		{
			if (!IsRunning)
			{
				// register callback to know immediatly when the bob connects
				Log.Write(Log.Level.Debug, "BC registering callback");
				if (!Util.Execute(FilePath.StartTsBot))
				{
					Log.Write(Log.Level.Debug, "BC callback canceled");
					return;
				}
				QueryConnection.OnClientConnect += AwaitBobConnect;
				WhisperChannelSubscribe(4);
				Log.Write(Log.Level.Debug, "BC now we are waiting for the bob");
			}
			if (IsTimingOut)
				cancellationTokenSource.Cancel();
		}

		public void Stop()
		{
			Log.Write(Log.Level.Info, "BC Stopping Bob");
			SendMessage("exit");
			IsRunning = false;
			whisperChannel.Clear();
			commandQueue.Clear();
			Log.Write(Log.Level.Debug, "BC bob is now officially dead");
			if (IsTimingOut)
				cancellationTokenSource.Cancel();
		}

		public void WhisperChannelSubscribe(int channel)
		{
			if (whisperChannel.Contains(channel))
				return;
			SendMessage("whisper channel add " + channel);
			whisperChannel.Add(channel);
		}

		public void WhisperChannelUnsubscribe(int channel)
		{
			if (!whisperChannel.Contains(channel))
				return;
			SendMessage("whisper channel remove " + channel);
			whisperChannel.Remove(channel);
		}

		private async void AwaitBobConnect(object sender, ClientEnterView e)
		{
			Log.Write(Log.Level.Debug, "BC user entered with GrId {0}", e.ServerGroups);
			if (e.ServerGroups.ToIntArray().Contains(data.bobGroupId))
			{
				Log.Write(Log.Level.Debug, "BC user with correct UID found");
				bobClient = await QueryConnection.GetClientById(e.Id);
				QueryConnection.OnClientConnect -= AwaitBobConnect;
				IsRunning = true;
				Log.Write(Log.Level.Debug, "BC bob is now officially running");
				await SendQueue();
			}
		}

		public void StartEndTimer()
		{
			HasUpdate();
			if (IsRunning)
			{
				if (IsTimingOut)
				{
					cancellationTokenSource.Cancel();
					Log.Write(Log.Level.Debug, "BC cTS raised");
					timerTask.Wait();
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
			timerTask = Task.Run(() =>
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
								Task.Delay(TimeSpan.FromSeconds(BOB_TIMEOUT - inactiveSeconds), cancellationToken).Wait();
						}
					}
					catch (TaskCanceledException)
					{
					}
					catch (AggregateException)
					{
					}
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
	}

	public struct BobControllerData
	{
		[InfoAttribute("ServerGroupID of the ServerBob")]
		public int bobGroupId;
	}
}