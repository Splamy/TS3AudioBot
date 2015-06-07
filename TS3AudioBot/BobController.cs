using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LockCheck;
using TeamSpeak3QueryApi.Net.Specialized.Responses;

namespace TS3AudioBot
{
	class BobController : IDisposable
	{
		/// <summary>
		/// After TIMEOUT seconds, the bob disconnects.
		/// </summary>
		private const int TIMEOUT = 60;
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

		public QueryConnection Query;
		public GetClientsInfo BobClient;

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
			lock (lockObject)
			{
				if (IsRunning)
				{
					//TODO Somehow we need to get a reference to the bob
					Query.TSClient.SendMessage(message, BobClient);
				}
			}
		}

		public void HasUpdate()
		{
			lastUpdate = DateTime.Now;
		}

		[LockCritical("lockObject")]
		public void Start()
		{
			lock (lockObject)
			{
				if (!IsRunning)
				{
					// Write own server query id into file
					string filepath = Path.Combine(data.folder, FILENAME);
					try
					{
						using (StreamWriter output = File.CreateText(filepath))
						{
							output.WriteLine(Query.TSClient.WhoAmI().Result.ClientId);
						}
					}
					catch (IOException ex)
					{
						Console.WriteLine("Can't open file {0}", filepath);
						Console.WriteLine(ex);
						return;
					}
					if (!Util.Execute(FilePath.StartTsBot))
						return;
					IsRunning = true;
				}
				if (IsRunning && IsTimingOut)
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
					timerTask.Wait();
				}
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
							if (inactiveSeconds > TIMEOUT)
							{
								Stop();
								break;
							}
							else
								Task.Delay(TimeSpan.FromSeconds(TIMEOUT - inactiveSeconds), cancellationToken).Wait();
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

		[LockCritical("lockObject")]
		public void Stop()
		{
			Log.Write(Log.Level.Info, "Stopping Bob");
			if (IsRunning)
			{
				// FIXME We should lock these two calls in between too
				SendMessage("exit");
				lock (lockObject)
				{
					IsRunning = false;
				}
			}
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