// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TSLib.Messages;
using IOFileInfo = System.IO.FileInfo;

namespace TSLib
{
	partial class TsBaseFunctions
	{
		/// <summary>Initiate a file upload to the server.</summary>
		/// <param name="file">Local file to upload.</param>
		/// <param name="channel">The channel id to upload to.</param>
		/// <param name="path">The upload path within the channel. Eg: "file.txt", "path/file.png"</param>
		/// <param name="overwrite">True if the upload should overwrite the file if it exists.
		/// False will throw an exception if the file already exists.</param>
		/// <param name="channelPassword">The password for the channel.</param>
		/// <returns>A token to track the file transfer.</returns>
		public Task<R<FileTransferToken, CommandError>> UploadFile(IOFileInfo file, ChannelId channel, string path, bool overwrite = false, string channelPassword = "")
			=> UploadFile(file.Open(FileMode.Open, FileAccess.Read), channel, path, overwrite, channelPassword);

		/// <summary>Initiate a file upload to the server.</summary>
		/// <param name="stream">Data stream to upload.</param>
		/// <param name="channel">The channel id to upload to.</param>
		/// <param name="path">The upload path within the channel. Eg: "file.txt", "path/file.png"</param>
		/// <param name="overwrite">True if the upload should overwrite the file if it exists.
		/// False will throw an exception if the file already exists.</param>
		/// <param name="channelPassword">The password for the channel.</param>
		/// <param name="closeStream">True will <see cref="IDisposable.Dispose"/> the stream after the upload is finished.</param>
		/// <param name="createMd5">Will generate a md5 sum of the uploaded file.</param>
		/// <returns>A token to track the file transfer.</returns>
		public async Task<R<FileTransferToken, CommandError>> UploadFile(Stream stream, ChannelId channel, string path, bool overwrite = false, string channelPassword = "", bool closeStream = true, bool createMd5 = false)
		{
			ushort cftid = GetFreeTransferId();
			var request = await FileTransferInitUpload(channel, path, channelPassword, cftid, stream.Length, overwrite, false);
			if (!request.Ok)
			{
				if (closeStream)
				{
#if NETSTANDARD2_0
					stream.Dispose();
#else
					await stream.DisposeAsync();
#endif
				}
				return request.Error;
			}
			var token = new FileTransferToken(stream, request.Value, channel, path, channelPassword, stream.Length, createMd5) { CloseStreamWhenDone = closeStream };
			return await Transfer(token);
		}

		/// <summary>Initiate a file download from the server.</summary>
		/// <param name="file">Local file to save to.</param>
		/// <param name="channel">The channel id to download from.</param>
		/// <param name="path">The download path within the channel. Eg: "file.txt", "path/file.png"</param>
		/// <param name="channelPassword">The password for the channel.</param>
		/// <returns>A token to track the file transfer.</returns>
		public Task<R<FileTransferToken, CommandError>> DownloadFile(IOFileInfo file, ChannelId channel, string path, string channelPassword = "")
			=> DownloadFile(file.Open(FileMode.Create, FileAccess.Write), channel, path, channelPassword, true);

		/// <summary>Initiate a file download from the server.</summary>
		/// <param name="stream">Data stream to write to.</param>
		/// <param name="channel">The channel id to download from.</param>
		/// <param name="path">The download path within the channel. Eg: "file.txt", "path/file.png"</param>
		/// <param name="channelPassword">The password for the channel.</param>
		/// <param name="closeStream">True will <see cref="IDisposable.Dispose"/> the stream after the download is finished.</param>
		/// <returns>A token to track the file transfer.</returns>
		public async Task<R<FileTransferToken, CommandError>> DownloadFile(Stream stream, ChannelId channel, string path, string channelPassword = "", bool closeStream = true)
		{
			ushort cftid = GetFreeTransferId();
			var request = await FileTransferInitDownload(channel, path, channelPassword, cftid, 0);
			if (!request.Ok)
			{
				if (closeStream)
				{
#if NETSTANDARD2_0
					stream.Dispose();
#else
					await stream.DisposeAsync();
#endif
				}
				return request.Error;
			}
			var token = new FileTransferToken(stream, request.Value, channel, path, channelPassword, 0) { CloseStreamWhenDone = closeStream };
			return await Transfer(token);
		}

		private async Task<R<FileTransferToken, CommandError>> Transfer(FileTransferToken token)
		{
			try
			{
				if (remoteAddress is null)
				{
					token.Status = TransferStatus.Failed;
					Log.Trace("Client is not connected. Transfer failed {@token}", token);
					return CommandError.ConnectionClosed;
				}
				if (token.Status != TransferStatus.Waiting)
					return CommandError.Custom("Token is not open");
				token.Status = TransferStatus.Transfering;

				Log.Trace("Creating new file transfer connection to {0}", remoteAddress);
				using var client = new TcpClient(remoteAddress.AddressFamily);
				try { await client.ConnectAsync(remoteAddress.Address, token.Port); }
				catch (SocketException ex)
				{
					Log.Warn(ex, "SocketException trying to connect to filetransfer port");
					token.Status = TransferStatus.Failed;
					return CommandError.ConnectionClosed;
				}
				using var md5Dig = token.CreateMd5 ? MD5.Create() : null;
				using var stream = client.GetStream();
				byte[] keyBytes = Encoding.ASCII.GetBytes(token.TransferKey);
				await stream.WriteAsync(keyBytes, 0, keyBytes.Length);

				if (token.SeekPosition >= 0 && token.LocalStream.Position != token.SeekPosition)
					token.LocalStream.Seek(token.SeekPosition, SeekOrigin.Begin);

				if (token.Direction == TransferDirection.Upload)
				{
					// https://referencesource.microsoft.com/#mscorlib/system/io/stream.cs,2a0f078c2e0c0aa8,references
					const int bufferSize = 81920;
					var buffer = new byte[bufferSize];
					int read;
					md5Dig?.Initialize();
					while ((read = await token.LocalStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
					{
						await stream.WriteAsync(buffer, 0, read);
						md5Dig?.TransformBlock(buffer, 0, read, buffer, 0);
					}
					md5Dig?.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
					token.Md5Sum = md5Dig?.Hash;
				}
				else // Download
				{
					// try to preallocate space
					try { token.LocalStream.SetLength(token.Size); }
					catch (NotSupportedException) { }

					await stream.CopyToAsync(token.LocalStream);
				}
				if (token.Status == TransferStatus.Transfering && token.LocalStream.Position == token.Size)
				{
					token.Status = TransferStatus.Done;
					if (token.CloseStreamWhenDone)
					{
#if NETSTANDARD2_0
						token.LocalStream.Dispose();
#else
						await token.LocalStream.DisposeAsync();
#endif
					}

				}
			}
			catch (IOException ex)
			{
				Log.Debug(ex, "IOException during filetransfer");
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Exception during filetransfer");
			}
			finally
			{
				if (token.Status != TransferStatus.Done && token.Status != TransferStatus.Cancelled)
					token.Status = TransferStatus.Failed;
			}
			if (token.Status == TransferStatus.Failed)
				return CommandError.Custom("Upload didn't finish");
			return token;
		}

		private ushort GetFreeTransferId() => unchecked(++transferIdCnt);

		/// <summary>Resumes a download from a previously stopped position.</summary>
		/// <param name="token">The aborted token.</param>
		public async Task<E<CommandError>> Resume(FileTransferToken token)
		{
			if (token.Status != TransferStatus.Cancelled)
				return CommandError.Custom("Only cancelled transfers can be resumed");

			if (token.Direction == TransferDirection.Upload)
			{
				var result = await FileTransferInitUpload(token.ChannelId, token.Path, token.ChannelPassword, token.ClientTransferId, token.Size, false, true);
				if (!result.Ok)
					return result.Error;
				var request = result.Value;
				token.ServerTransferId = request.ServerFileTransferId;
				token.SeekPosition = (long)request.SeekPosition;
				token.Port = request.Port;
				token.TransferKey = request.FileTransferKey;
			}
			else // Download
			{
				var result = await FileTransferInitDownload(token.ChannelId, token.Path, token.ChannelPassword, token.ClientTransferId, token.LocalStream.Position);
				if (!result.Ok)
					return result.Error;
				var request = result.Value;
				token.ServerTransferId = request.ServerFileTransferId;
				token.SeekPosition = -1;
				token.Port = request.Port;
				token.TransferKey = request.FileTransferKey;
			}

			token.Status = TransferStatus.Waiting;

			return await Transfer(token);
		}

		/// <summary>Stops an active transfer.</summary>
		/// <param name="token">The token to abort.</param>
		/// <param name="delete">True to delete the file.
		/// False to only temporarily stop the transfer (can be resumed again with <see cref="Resume"/>).</param>
		public async Task Abort(FileTransferToken token, bool delete = false)
		{
			if (token.Status != TransferStatus.Transfering && token.Status != TransferStatus.Waiting)
				return;
			await FileTransferStop(token.ServerTransferId, delete);
			token.Status = TransferStatus.Cancelled;
			if (delete && token.CloseStreamWhenDone)
			{
#if NETSTANDARD2_0
				token.LocalStream.Dispose();
#else
				await token.LocalStream.DisposeAsync();
#endif
			}
		}

		/// <summary>Gets information about the current transfer status.</summary>
		/// <param name="token">The transfer to check.</param>
		/// <returns>Returns an information object or <code>null</code> when not available.</returns>
		public async Task<R<FileTransfer, CommandError>> GetStats(FileTransferToken token)
		{
			if (token.Status != TransferStatus.Transfering)
				return CommandError.Custom("No transfer found");

			var result = await FileTransferList();
			if (result.Ok)
				return result.Value.Where(x => x.ServerFileTransferId == token.ServerTransferId).MapToSingle();
			return result.Error;
		}
	}

	/// <summary>Points to a file transfer.
	/// This token can be used to further interact with a transfer via the <see cref="FileTransferManager"/>.</summary>
	public sealed class FileTransferToken
	{
		public Stream LocalStream { get; }
		public TransferDirection Direction { get; }
		public ChannelId ChannelId { get; }
		public string Path { get; }
		public long Size { get; }
		public ushort ClientTransferId { get; }
		public ushort ServerTransferId { get; internal set; }
		public string ChannelPassword { get; set; }
		public ushort Port { get; internal set; }
		public long SeekPosition { get; internal set; }
		public string TransferKey { get; internal set; }
		public bool CloseStreamWhenDone { get; set; }
		public bool CreateMd5 { get; }
		public byte[]? Md5Sum { get; internal set; }
		public TransferStatus Status { get; set; }

		public FileTransferToken(Stream localStream, FileUpload upload, ChannelId channelId,
			string path, string channelPassword, long size, bool createMd5)
			: this(localStream, upload.ClientFileTransferId, upload.ServerFileTransferId, TransferDirection.Upload,
				channelId, path, channelPassword, upload.Port, (long)upload.SeekPosition, upload.FileTransferKey, size, createMd5)
		{ }

		public FileTransferToken(Stream localStream, FileDownload download, ChannelId channelId,
			string path, string channelPassword, long seekPos)
			: this(localStream, download.ClientFileTransferId, download.ServerFileTransferId, TransferDirection.Download,
				channelId, path, channelPassword, download.Port, seekPos, download.FileTransferKey, (long)download.Size, false)
		{ }

		public FileTransferToken(Stream localStream, ushort cftid, ushort sftid,
			TransferDirection dir, ChannelId channelId, string path, string channelPassword, ushort port, long seekPos,
			string transferKey, long size, bool createMd5)
		{
			CloseStreamWhenDone = false;
			Status = TransferStatus.Waiting;
			LocalStream = localStream;
			Direction = dir;
			ClientTransferId = cftid;
			ServerTransferId = sftid;
			ChannelId = channelId;
			Path = path;
			ChannelPassword = channelPassword;
			Port = port;
			SeekPosition = seekPos;
			TransferKey = transferKey;
			Size = size;
			CreateMd5 = createMd5;
		}
	}

	public enum TransferDirection
	{
		Upload,
		Download,
	}

	public enum TransferStatus
	{
		Waiting,
		Transfering,
		Done,
		Cancelled,
		Failed,
	}
}
