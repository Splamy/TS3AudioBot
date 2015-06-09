using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TeamSpeak3QueryApi.Net.Specialized;
using TeamSpeak3QueryApi.Net.Specialized.Responses;
using TeamSpeak3QueryApi.Net.Specialized.Notifications;

namespace TS3AudioBot
{
	class PrivateSession
	{
		GetClientsInfo client;
		AudioRessource userRessources;
		Func<TextMessage, Task<bool>> awaitingResponse = null;
	}
}
