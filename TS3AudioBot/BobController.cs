using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LockCheck;

namespace TS3AudioBot
{
	class BobController : IDisposable
	{
		/// <summary>
		/// After TIMEOUT seconds, the bob disconnects.
		/// </summary>
		private const int TIMEOUT = 60;

		private BobControllerData data;
		private Task timerTask;
		private CancellationTokenSource cancellationTokenSource;
		private CancellationToken cancellationToken;
		private DateTime lastUpdate = DateTime.Now;
		private bool quality = false;
		private bool sending = false;

		private StreamWriter outStream;

		private readonly object lockObject = new object();

		public bool IsRunning
		{
			get { return outStream != null; }
		}

		public bool IsTimingOut
		{
			get { return timerTask != null && !timerTask.IsCompleted; }
		}

		public bool Quality
		{
			get { return quality; }
			set
			{
				quality = value;
				SendMessage("quality " + (value ? "on" : "off"));
			}
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
				if (outStream != null)
				{
					outStream.Write(message);
					outStream.Write('\n');
					outStream.Flush();
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
				if (!IsRunning && Util.Execute(FilePath.StartTsBot))
				{
					// Wait some time to increase the chance that the Bob is running
					Task.Delay(1000).Wait();
					FileInfo info = new FileInfo(data.file);
					if (!info.Exists)
					{
						Console.WriteLine("Can't open file {0}", data.file);
						return;
					}
					try
					{
						outStream = new StreamWriter(info.OpenWrite());
					}
					catch (IOException ex)
					{
						Console.WriteLine("Can't open the file {0} ({1})", data.file, ex);
						outStream = null;
						return;
					}
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
					catch (TaskCanceledException) { }
					catch (AggregateException) { }
				}, cancellationToken);
		}

		[LockCritical("lockObject")]
		public void Stop()
		{
			Log.Write(Log.Level.Info, "Stopping Bob...");
			if (IsRunning)
			{
				Quality = false;
				SendMessage("exit");
				lock (lockObject)
				{
					outStream.Close();
					outStream = null;
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
		[InfoAttribute("the pipe file for communication between the TS3AudioBot and the TeamSpeak3 Client plugin")]
		public string file;
	}
}