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
					Task.Delay(30, cancellationToken).Wait();
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
					try
					{
						outStream = new StreamWriter(File.Open(data.File, FileMode.Append));
					}
					catch (IOException ex)
					{
						Console.WriteLine("Can't open the file {0} ({1})", data.File, ex);
						return;
					}

					if (!timerTask.IsCompleted)
					{
						cancellationTokenSource.Cancel();
						timerTask.Wait();
					}
					cancellationTokenSource = new CancellationTokenSource();
					cancellationToken = cancellationTokenSource.Token;
					timerTask = Task.Run(Timer, cancellationToken);
				}
			}
		}

		public void Stop()
		{
			lock (lockObject)
			{
				if (outStream != null)
				{
					Console.WriteLine("Stoping Bob...");
					outStream.WriteLine("exit");
					outStream.Close();
					outStream = null;
				}
				if (cancellationToken.CanBeCanceled)
				{
					cancellationTokenSource.Cancel();
					timerTask.Wait();
				}
			}
		}

		public void SetAudio(bool isOn)
		{
			lock (lockObject)
			{
				if (outStream != null)
					outStream.WriteLine("audio " + (isOn ? "on" : "off"));
			}
		}

		public void SetQuality(bool isOn)
		{
			lock (lockObject)
			{
				if (outStream != null)
					outStream.WriteLine("quality " + (isOn ? "on" : "off"));
			}
		}
	}

	public struct BobControllerData
	{
		public string File;
	}
}