using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3AudioBot
{
	interface IPlayerConnection
	{
		void Start();
		void Close();

		bool IsPlaying();
		int GetLength();
		int GetPosition();
		void SetPosition(int position);
		void SetLoop(bool enabled);

		void AudioStop();
		void AudioStart(string url);
	}
}
