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
		private readonly Func<HttpResponse, bool> writeFunc;

		public DataStream(Func<HttpResponse, bool> writeFunc)
		{
			this.writeFunc = writeFunc;
		}

		public bool WriteOut(HttpResponse response) => writeFunc(response);

		public override string ToString() => null;
	}
}
