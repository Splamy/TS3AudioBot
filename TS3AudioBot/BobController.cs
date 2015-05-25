using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TS3AudioBot
{
	class BobController
	{
		BobControllerData data;
		Task timerTask;
		CancellationTokenSource cancellationTokenSource;
		CancellationToken cancellationToken;
		DateTime lastUpdate = DateTime.Now;

		StreamWriter outStream;

		readonly object lockObject = new object();

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

		public BobController(BobControllerData data)
		{
			this.data = data;
		}

		void Timer()
		{
			while (!cancellationToken.IsCancellationRequested && IsRunning)
			{
				double inactiveSeconds = (DateTime.Now - lastUpdate).TotalSeconds;
				if (inactiveSeconds > 30)
					Stop();
				else
					Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).Wait();
			}
		}

		void SendMessage(string message)
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
				if (!IsRunning && Util.Execute("StartTsBot.sh"))
				{
					FileInfo info = new FileInfo(data.File);
					if (!info.Exists)
					{
						Console.WriteLine("Can't open file {0}", data.File);
						return;
					}
					try
					{
						outStream = new StreamWriter(info.OpenWrite());
					}
					catch (IOException ex)
					{
						Console.WriteLine("Can't open the file {0} ({1})", data.File, ex);
						outStream = null;
						return;
					}

					if (!timerTask.IsCompleted)
					{
						cancellationTokenSource.Cancel();
						timerTask.Wait();
					}
					cancellationTokenSource = new CancellationTokenSource();
					cancellationToken = cancellationTokenSource.Token;
					timerTask = Task.Run((Action)Timer, cancellationToken);
				}
			}
		}

		public void Stop()
		{
			if (outStream != null)
			{
				Console.WriteLine("Stoping Bob...");
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

		public void SetAudio(bool isOn)
		{
			SendMessage("audio " + (isOn ? "on" : "off"));
		}

		public void SetQuality(bool isOn)
		{
			SendMessage("quality " + (isOn ? "on" : "off"));
		}
	}

	public struct BobControllerData
	{
		public string File;
	}
}