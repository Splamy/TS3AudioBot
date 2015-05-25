using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3AudioBot
{
	interface IPlayerConnection
	{
		Action<string> TextCallback { get; set; }
		void Start();
		void Close();

		void AudioStop();
		void AudioPlay(string url);
		void AudioLogin();
		void SendCommandRaw(string msg);
	}
}
