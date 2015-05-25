using System;
using System.IO;

namespace TS3AudioBot
{
	public class BobController
	{
		BobControllerData data;

		StreamWriter outStream;

		public BobController(BobControllerData data)
		{
			this.data = data;
		}

		public void Start()
		{
			if (!IsRunning() && Util.Execute("StartTsBot.sh"))
			{
				try
				{
					outStream = new StreamWriter(File.Open(data.File, FileMode.Append));
				}
				catch (IOException ex)
				{
					Console.WriteLine("Can't open the file {0} ({1})", data.File, ex);
				}
			}
		}

		public void Stop()
		{
			if (outStream != null)
			{
				Console.WriteLine("Stoping Bob...");
				outStream.WriteLine("exit");
				outStream.Close();
				outStream = null;
			}
		}

		public void SetAudio(bool isOn)
		{
			if (outStream != null)
				outStream.WriteLine("audio " + (isOn ? "on" : "off"));
		}

		public void SetQuality(bool isOn)
		{
			if (outStream != null)
				outStream.WriteLine("quality " + (isOn ? "on" : "off"));
		}

		public bool IsRunning()
		{
			return outStream != null;
		}
	}

	public struct BobControllerData
	{
		public string File;
	}
}