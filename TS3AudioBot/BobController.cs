using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TS3AudioBot
{
	class BobController
	{
		private BobControllerData data;
		private Task timerTask;
		private CancellationTokenSource cancellationTokenSource;
		private CancellationToken cancellationToken;
		private DateTime lastUpdate = DateTime.Now;
		private bool quality = false;
		private bool sending = true;

		private StreamWriter outStream;

		private readonly object lockObject = new object();

		public bool IsRunning
		{
			get
			{
				lock (lockObject)
				{
					return outStream != null;
				}
			}
		}

		public bool Quality
		{
			get { return quality; }
			set
			{
				if (quality != value)
				{
					quality = value;
					SendMessage("quality " + (value ? "on" : "off"));
				}
			}
		}

		public bool Sending
		{
			get { return sending; }
			set
			{
				if (sending != value)
				{
					sending = value;
					SendMessage("audio " + (value ? "on" : "off"));
				}
			}
		}

		public BobController(BobControllerData data)
		{
			this.data = data;
		}

		private void Timer()
		{
			while (!cancellationToken.IsCancellationRequested && IsRunning)
			{
				double inactiveSeconds = (DateTime.Now - lastUpdate).TotalSeconds;
				if (inactiveSeconds > 30)
					Stop();
				else
					Task.Delay(TimeSpan.FromSeconds(30 - inactiveSeconds), cancellationToken).Wait();
			}
		}

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

		public void Start()
		{
			lock (lockObject)
			{
				if (!IsRunning && Util.Execute(SubTask.StartTsBot))
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

					if (timerTask != null && !timerTask.IsCompleted && cancellationToken.CanBeCanceled)
						cancellationTokenSource.Cancel();
				}
			}
		}

		public void StartEndTimer()
		{
			if (timerTask != null && !timerTask.IsCompleted)
			{
				if (cancellationToken.CanBeCanceled)
					cancellationTokenSource.Cancel();
				timerTask.Wait();
			}
			cancellationTokenSource = new CancellationTokenSource();
			cancellationToken = cancellationTokenSource.Token;
			timerTask = Task.Run((Action)Timer, cancellationToken);
		}

		public void Stop()
		{
			if (outStream != null)
			{
				Console.WriteLine("Stopping Bob...");
				Quality = false;
				SendMessage("exit");
				lock (lockObject)
				{
					outStream.Close();
					outStream = null;
				}
			}
			if (cancellationToken.CanBeCanceled)
			{
				cancellationTokenSource.Cancel();
				timerTask.Wait();
			}
		}
	}

	public struct BobControllerData
	{
		[InfoAttribute("the pipe file for communication between the TS3AudioBot and the TeamSpeak3 Client plugin")]
		public string file;
	}
}