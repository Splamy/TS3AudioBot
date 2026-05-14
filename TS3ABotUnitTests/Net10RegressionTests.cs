using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Environment;
using TS3AudioBot.Web.Api;

namespace TS3ABotUnitTests
{
	[TestFixture]
	public class Net10RegressionTests
	{
		[Test]
		public void BuildRequestUrl_UsesRawTargetWhenAvailable()
		{
			var uri = WebApi.BuildRequestUrl("/api/help?scope=all", PathString.Empty, "/api/bot/list", QueryString.Empty);

			Assert.AreEqual("/api/help?scope=all", uri.PathAndQuery);
		}

		[Test]
		public void BuildRequestUrl_ReconstructsApiPathsWhenRawTargetIsMissing()
		{
			var helpUri = WebApi.BuildRequestUrl(null, PathString.Empty, "/api/help", QueryString.Empty);
			var botListUri = WebApi.BuildRequestUrl(null, PathString.Empty, "/api/bot/list", QueryString.Empty);

			Assert.AreEqual("/api/help", helpUri.PathAndQuery);
			Assert.AreEqual("/api/bot/list", botListUri.PathAndQuery);
		}

		[Test]
		public async Task WriteResponseBodyAsync_UsesAsynchronousBodyWrites()
		{
			var context = new DefaultHttpContext();
			var stream = new AsyncOnlyWriteStream();
			context.Response.Body = stream;

			await WebApi.WriteResponseBodyAsync(context.Response, "{\"ok\":true}");

			Assert.IsFalse(stream.SyncWriteCalled);
			Assert.AreEqual("{\"ok\":true}", stream.GetWrittenText());
		}

		[Test]
		public void ParseNetRuntimeDescription_ParsesModernDotNetDescriptions()
		{
			var runtime = SystemData.ParseNetRuntimeDescription(".NET 10.0.3");

			Assert.NotNull(runtime);
			Assert.AreEqual(Runtime.Core, runtime.Runtime);
			Assert.AreEqual(".NET (10.0.3)", runtime.FullName);
			Assert.AreEqual(new Version(10, 0, 3, 0), runtime.SemVer);
		}

		[Test]
		public void ParseNetRuntimeDescription_DoesNotTreatDotNetFrameworkAsDotNetCore()
		{
			Assert.IsNull(SystemData.ParseNetRuntimeDescription(".NET Framework 4.8.1"));
		}

		[Test]
		public void RuntimeData_DoesNotReportDotNetFrameworkOnNet10()
		{
			Assert.AreEqual(Runtime.Core, SystemData.RuntimeData.Runtime);
			Assert.IsTrue(SystemData.RuntimeData.FullName.StartsWith(".NET (", StringComparison.Ordinal));
			Assert.IsFalse(SystemData.RuntimeData.FullName.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase));
		}

		private sealed class AsyncOnlyWriteStream : Stream
		{
			private readonly MemoryStream inner = new MemoryStream();

			public bool SyncWriteCalled { get; private set; }

			public string GetWrittenText() => Encoding.UTF8.GetString(inner.ToArray());

			public override bool CanRead => false;
			public override bool CanSeek => false;
			public override bool CanWrite => true;
			public override long Length => inner.Length;
			public override long Position { get => inner.Position; set => throw new NotSupportedException(); }

			public override void Flush() { }
			public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
			public override void SetLength(long value) => inner.SetLength(value);

			public override void Write(byte[] buffer, int offset, int count)
			{
				SyncWriteCalled = true;
				throw new InvalidOperationException("Synchronous writes are not allowed in this test.");
			}

			public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
			{
				return inner.WriteAsync(buffer, cancellationToken);
			}

			public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
			{
				return inner.WriteAsync(buffer, offset, count, cancellationToken);
			}
		}
	}
}
