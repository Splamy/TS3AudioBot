// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client
{
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net.Sockets;
	using System.Text;
	using System.Threading;

	using ClientUidT = System.String;
	using ClientDbIdT = System.UInt64;
	using ClientIdT = System.UInt16;
	using ChannelIdT = System.UInt64;
	using ServerGroupIdT = System.UInt64;
	using ChannelGroupIdT = System.UInt64;

	/// <summary>Queues and manages up- and downloads.</summary>
	public sealed class FileTransferManager
	{
		private readonly Ts3BaseFunctions parent;
		private readonly Queue<FileTransferToken> transferQueue;
		private Thread workerThread;
		private bool threadEnd;
		private ushort transferIdCnt;

		public FileTransferManager(Ts3BaseFunctions ts3Connection)
		{
			parent = ts3Connection;
			//ts3connection.OnFileTransferStatus += FileStatusNotification;
			Util.Init(ref transferQueue);
		}

		/// <summary>Initiate a file upload to the server.</summary>
		/// <param name="file">Local file to upload.</param>
		/// <param name="channel">The channel id to upload to.</param>
		/// <param name="path">The upload path within the channel. Eg: "file.txt", "path/file.png"</param>
		/// <param name="overwrite">True if the upload should overwrite the file if it exists.
		/// False will throw an exception if the file already exists.</param>
		/// <param name="channelPassword">The password for the channel.</param>
		/// <returns>A token to track the file transfer.</returns>
		public FileTransferToken UploadFile(FileInfo file, ChannelIdT channel, string path, bool overwrite = false, string channelPassword = "")
			=> UploadFile(file.Open(FileMode.Open, FileAccess.Read), channel, path, overwrite, channelPassword);

		/// <summary>Initiate a file upload to the server.</summary>
		/// <param name="stream">Data stream to upload.</param>
		/// <param name="channel">The channel id to upload to.</param>
		/// <param name="path">The upload path within the channel. Eg: "file.txt", "path/file.png"</param>
		/// <param name="overwrite">True if the upload should overwrite the file if it exists.
		/// False will throw an exception if the file already exists.</param>
		/// <param name="channelPassword">The password for the channel.</param>
		/// <param name="closeStream">True will <see cref="IDisposable.Dispose"/> the stream after the upload is finished.</param>
		/// <returns>A token to track the file transfer.</returns>
		public FileTransferToken UploadFile(Stream stream, ChannelIdT channel, string path, bool overwrite = false, string channelPassword = "", bool closeStream = false)
		{
			ushort cftid = GetFreeTransferId();
			var request = parent.FileTransferInitUpload(channel, path, channelPassword, cftid, stream.Length, overwrite, false);
			if (!string.IsNullOrEmpty(request.Message))
				throw new Ts3Exception(request.Message);
			var token = new FileTransferToken(stream, request, channel, path, channelPassword, stream.Length) { CloseStreamWhenDone = closeStream };
			StartWorker(token);
			return token;
		}

		/// <summary>Initiate a file download from the server.</summary>
		/// <param name="file">Local file to save to.</param>
		/// <param name="channel">The channel id to download from.</param>
		/// <param name="path">The download path within the channel. Eg: "file.txt", "path/file.png"</param>
		/// <param name="channelPassword">The password for the channel.</param>
		/// <returns>A token to track the file transfer.</returns>
		public FileTransferToken DownloadFile(FileInfo file, ChannelIdT channel, string path, string channelPassword = "")
			=> DownloadFile(file.Open(FileMode.Create, FileAccess.Write), channel, path, channelPassword, true);

		/// <summary>Initiate a file download from the server.</summary>
		/// <param name="stream">Data stream to write to.</param>
		/// <param name="channel">The channel id to download from.</param>
		/// <param name="path">The download path within the channel. Eg: "file.txt", "path/file.png"</param>
		/// <param name="channelPassword">The password for the channel.</param>
		/// <param name="closeStream">True will <see cref="IDisposable.Dispose"/> the stream after the download is finished.</param>
		/// <returns>A token to track the file transfer.</returns>
		public FileTransferToken DownloadFile(Stream stream, ChannelIdT channel, string path, string channelPassword = "", bool closeStream = false)
		{
			ushort cftid = GetFreeTransferId();
			var request = parent.FileTransferInitDownload(channel, path, channelPassword, cftid, 0);
			if (!string.IsNullOrEmpty(request.Message))
				throw new Ts3Exception(request.Message);
			var token = new FileTransferToken(stream, request, channel, path, channelPassword, 0) { CloseStreamWhenDone = closeStream };
			StartWorker(token);
			return token;
		}

		private void StartWorker(FileTransferToken token)
		{
			lock (transferQueue)
			{
				transferQueue.Enqueue(token);

				if (threadEnd || workerThread == null || !workerThread.IsAlive)
				{
					threadEnd = false;
					workerThread = new Thread(TransferLoop);
					workerThread.Start();
				}
			}
		}

		private ushort GetFreeTransferId()
		{
			return unchecked(++transferIdCnt);
		}

		/// <summary>Resumes a download from a previously stopped position.</summary>
		/// <param name="token">The aborted token.</param>
		public void Resume(FileTransferToken token)
		{
			lock (token)
			{
				if (token.Status != TransferStatus.Cancelled)
					throw new Ts3Exception("Only cancelled transfers can be resumed");

				if (token.Direction == TransferDirection.Upload)
				{
					var request = parent.FileTransferInitUpload(token.ChannelId, token.Path, token.ChannelPassword, token.ClientTransferId, token.Size, false, true);
					if (!string.IsNullOrEmpty(request.Message))
						throw new Ts3Exception(request.Message);
					token.ServerTransferId = request.ServerFileTransferId;
					token.SeekPosition = request.SeekPosistion;
					token.Port = request.Port;
					token.TransferKey = request.FileTransferKey;
				}
				else // Download
				{
					var request = parent.FileTransferInitDownload(token.ChannelId, token.Path, token.ChannelPassword, token.ClientTransferId, token.LocalStream.Position);
					if (!string.IsNullOrEmpty(request.Message))
						throw new Ts3Exception(request.Message);
					token.ServerTransferId = request.ServerFileTransferId;
					token.SeekPosition = -1;
					token.Port = request.Port;
					token.TransferKey = request.FileTransferKey;
				}
				token.Status = TransferStatus.Waiting;
			}
			StartWorker(token);
		}

		/// <summary>Stops an active transfer.</summary>
		/// <param name="token">The token to abort.</param>
		/// <param name="delete">True to delete the file.
		/// False to only temporarily stop the transfer (can be resumed again with <see cref="Resume"/>).</param>
		public void Abort(FileTransferToken token, bool delete = false)
		{
			lock (token)
			{
				if (token.Status != TransferStatus.Trasfering && token.Status != TransferStatus.Waiting)
					return;
				parent.FileTransferStop(token.ServerTransferId, delete);
				token.Status = TransferStatus.Cancelled;
				if (delete && token.CloseStreamWhenDone)
				{
					token.LocalStream.Close();
				}
			}
		}

		/// <summary>Gets information about the current transfer status.</summary>
		/// <param name="token">The transfer to check.</param>
		/// <returns>Returns an information object or <code>null</code> when not available.</returns>
		public FileTransfer GetStats(FileTransferToken token)
		{
			lock (token)
			{
				if (token.Status != TransferStatus.Trasfering)
					return null;
			}
			try { return parent.FileTransferList().FirstOrDefault(x => x.ServerFileTransferId == token.ServerTransferId); }
			// catch case when transfer is not found (probably already over or not yet started)
			catch (Ts3CommandException ts3ex) when (ts3ex.ErrorStatus.Id == Ts3ErrorCode.database_empty_result) { return null; }
		}

		private void TransferLoop()
		{
			while (true)
			{
				FileTransferToken token;
				lock (transferQueue)
				{
					if (transferQueue.Count <= 0)
					{
						threadEnd = true;
						break;
					}
					token = transferQueue.Dequeue();
				}

				try
				{
					lock (token)
					{
						if (token.Status != TransferStatus.Waiting)
							continue;
						token.Status = TransferStatus.Trasfering;
					}

					using (var client = new TcpClient(AddressFamily.InterNetworkV6))
					{
						client.Client.DualMode = true;
						try { client.Connect(parent.remoteAddress.Address, token.Port); }
						catch (SocketException)
						{
							token.Status = TransferStatus.Failed;
							continue;
						}
						using (var stream = client.GetStream())
						{
							byte[] keyBytes = Encoding.ASCII.GetBytes(token.TransferKey);
							stream.Write(keyBytes, 0, keyBytes.Length);

							if (token.SeekPosition >= 0 && token.LocalStream.Position != token.SeekPosition)
								token.LocalStream.Seek(token.SeekPosition, SeekOrigin.Begin);

							if (token.Direction == TransferDirection.Upload)
							{
								token.LocalStream.CopyTo(stream);
							}
							else // Download
							{
								// try to preallocate space
								try { token.LocalStream.SetLength(token.Size); }
								catch (NotSupportedException) { }

								stream.CopyTo(token.LocalStream);
							}
							lock (token)
							{
								if (token.Status == TransferStatus.Trasfering && token.LocalStream.Position == token.Size)
								{
									token.Status = TransferStatus.Done;
									if (token.CloseStreamWhenDone)
										token.LocalStream.Close();
								}
							}
						}
					}
				}
				catch (IOException) { }
				finally
				{
					lock (token)
					{
						if (token.Status != TransferStatus.Done && token.Status != TransferStatus.Cancelled)
							token.Status = TransferStatus.Failed;
					}
				}
			}
		}
	}

	/// <summary>Points to a file transfer.
	/// This token can be used to further interact with a transfer via the <see cref="FileTransferManager"/>.</summary>
	public sealed class FileTransferToken
	{
		public Stream LocalStream { get; }
		public TransferDirection Direction { get; }
		public ChannelIdT ChannelId { get; }
		public string Path { get; }
		public long Size { get; }
		public ushort ClientTransferId { get; }
		public ushort ServerTransferId { get; internal set; }
		public string ChannelPassword { get; set; }
		public ushort Port { get; internal set; }
		public long SeekPosition { get; internal set; }
		public string TransferKey { get; internal set; }
		public bool CloseStreamWhenDone { get; set; }

		public TransferStatus Status { get; internal set; }

		public FileTransferToken(Stream localStream, FileUpload upload, ChannelIdT channelId,
			string path, string channelPassword, long size)
			: this(localStream, upload.ClientFileTransferId, upload.ServerFileTransferId, TransferDirection.Upload,
				channelId, path, channelPassword, upload.Port, upload.SeekPosistion, upload.FileTransferKey, size)
		{ }

		public FileTransferToken(Stream localStream, FileDownload download, ChannelIdT channelId,
			string path, string channelPassword, long seekPos)
			: this(localStream, download.ClientFileTransferId, download.ServerFileTransferId, TransferDirection.Download,
				channelId, path, channelPassword, download.Port, seekPos, download.FileTransferKey, download.Size)
		{ }

		public FileTransferToken(Stream localStream, ushort cftid, ushort sftid,
			TransferDirection dir, ChannelIdT channelId, string path, string channelPassword, ushort port, long seekPos,
			string transferKey, long size)
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
		}

		public void Wait()
		{
			while (Status == TransferStatus.Waiting || Status == TransferStatus.Trasfering)
				Thread.Sleep(10);
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
		Trasfering,
		Done,
		Cancelled,
		Failed,
	}
}
