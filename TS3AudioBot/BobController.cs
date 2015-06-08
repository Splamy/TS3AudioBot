using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LockCheck;
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
		/// <summary>
		/// The name of the file which is used to tell our own server client id to the Bob.
		/// </summary>
		private const string FILENAME = "queryId";

		private BobControllerData data;
		private Task timerTask;
		private CancellationTokenSource cancellationTokenSource;
		private CancellationToken cancellationToken;
		private DateTime lastUpdate = DateTime.Now;
		private bool sending = false;

		private readonly object lockObject = new object();
		public QueryConnection QueryConnection { get; set; }
		private GetClientsInfo bobClient;

		public bool IsRunning { get; private set; }

		public bool IsTimingOut
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
			this.data = data;
		}

		[LockCritical("lockObject")]
		private void SendMessage(string message)
		{
			if (bobClient == null)
			{
				Log.Write(Log.Level.Debug, "bobClient is null! Message is lost: {0}", message);
				return;
			}
			lock (lockObject)
			{
				if (IsRunning)
				{
					Log.Write(Log.Level.Debug, "BC sending to bobC: {0}", message);
					QueryConnection.TSClient.SendMessage(message, bobClient);
				}
			}
		}

		public void HasUpdate()
		{
			lastUpdate = DateTime.Now;
		}

		//[LockCritical("lockObject")]
		public async void Start()
		{
			if (!IsRunning)
			{
				// Write own server query id into file
				string filepath = Path.Combine(data.folder, FILENAME);
				Log.Write(Log.Level.Debug, "requesting whoAmI");
				WhoAmI whoAmI = await QueryConnection.TSClient.WhoAmI();
				Log.Write(Log.Level.Debug, "got whoAmI");
				string myId = whoAmI.ClientId.ToString();
				try
				{
										File.WriteAllText(filepath, myId, new UTF8Encoding(false));
				}
				catch (IOException ex)
				{
					Log.Write(Log.Level.Error, "Can't open file {0} ({1})", filepath, ex);
					return;
				}
				// register callback to know immediatly when the bob connects
				Log.Write(Log.Level.Debug, "Registering callback");
				QueryConnection.OnClientConnect += AwaitBobConnect;
				if (!Util.Execute(FilePath.StartTsBot))
				{
					QueryConnection.OnClientConnect -= AwaitBobConnect;
					Log.Write(Log.Level.Debug, "callback canceled");
					return;
				}
			}
		}

		private void AwaitBobConnect(object sender, ClientEnterView e)
		{
			Log.Write(Log.Level.Debug, "User entere with GrId {0}", e.ServerGroups);
			if (e.ServerGroups == "15")
			{
				Log.Write(Log.Level.Debug, "User with correct UID found");
				bobClient = QueryConnection.GetClientById(e.Id).Result;
				QueryConnection.OnClientConnect -= AwaitBobConnect;
				IsRunning = true;
				if (IsTimingOut)
					cancellationTokenSource.Cancel();
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
								Log.Write(Log.Level.Debug, "Timeout ran out...");
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

		//[LockCritical("lockObject")]
		public void Stop()
		{
			Log.Write(Log.Level.Info, "Stopping Bob");
			SendMessage("exit");
			IsRunning = false;
			if (IsTimingOut)
				cancellationTokenSource.Cancel();
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
		[InfoAttribute("the folder that contains the clientId file of this server query for " +
			"communication between the TS3AudioBot and the TeamSpeak3 Client plugin")]
		public string folder;
	}
}