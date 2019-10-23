using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3AudioBot.Web.Api
{
	public class DataStream
	{
		private Action<HttpResponse> writeFunc;

		public DataStream(Action<HttpResponse> writeFunc)
		{
			this.writeFunc = writeFunc;
		}

		public void WriteOut(HttpResponse response)
		{
			writeFunc(response);
		}
	}
}
