// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.
















namespace TS3Client.Full
{
	using Helper;
	using Messages;
	using System;

	public sealed partial class Ts3FullClient
	{
		
		public event NotifyEventHandler<ChannelChanged> OnChannelChanged;
		public event EventHandler<ChannelChanged> OnEachChannelChanged;
		public event NotifyEventHandler<ChannelCreated> OnChannelCreated;
		public event EventHandler<ChannelCreated> OnEachChannelCreated;
		public event NotifyEventHandler<ChannelDeleted> OnChannelDeleted;
		public event EventHandler<ChannelDeleted> OnEachChannelDeleted;
		public event NotifyEventHandler<ChannelEdited> OnChannelEdited;
		public event EventHandler<ChannelEdited> OnEachChannelEdited;
		public event NotifyEventHandler<ChannelGroupList> OnChannelGroupList;
		public event EventHandler<ChannelGroupList> OnEachChannelGroupList;
		public event NotifyEventHandler<ChannelList> OnChannelList;
		public event EventHandler<ChannelList> OnEachChannelList;
		public event NotifyEventHandler<ChannelListFinished> OnChannelListFinished;
		public event EventHandler<ChannelListFinished> OnEachChannelListFinished;
		public event NotifyEventHandler<ChannelMoved> OnChannelMoved;
		public event EventHandler<ChannelMoved> OnEachChannelMoved;
		public event NotifyEventHandler<ChannelPasswordChanged> OnChannelPasswordChanged;
		public event EventHandler<ChannelPasswordChanged> OnEachChannelPasswordChanged;
		public event NotifyEventHandler<ChannelSubscribed> OnChannelSubscribed;
		public event EventHandler<ChannelSubscribed> OnEachChannelSubscribed;
		public event NotifyEventHandler<ChannelUnsubscribed> OnChannelUnsubscribed;
		public event EventHandler<ChannelUnsubscribed> OnEachChannelUnsubscribed;
		public event NotifyEventHandler<ClientChannelGroupChanged> OnClientChannelGroupChanged;
		public event EventHandler<ClientChannelGroupChanged> OnEachClientChannelGroupChanged;
		public event NotifyEventHandler<ClientChatComposing> OnClientChatComposing;
		public event EventHandler<ClientChatComposing> OnEachClientChatComposing;
		public event NotifyEventHandler<ClientDbIdFromUid> OnClientDbIdFromUid;
		public event EventHandler<ClientDbIdFromUid> OnEachClientDbIdFromUid;
		public override event NotifyEventHandler<ClientEnterView> OnClientEnterView;
		public event EventHandler<ClientEnterView> OnEachClientEnterView;
		public event NotifyEventHandler<ClientIds> OnClientIds;
		public event EventHandler<ClientIds> OnEachClientIds;
		public override event NotifyEventHandler<ClientLeftView> OnClientLeftView;
		public event EventHandler<ClientLeftView> OnEachClientLeftView;
		public event NotifyEventHandler<ClientMoved> OnClientMoved;
		public event EventHandler<ClientMoved> OnEachClientMoved;
		public event NotifyEventHandler<ClientNeededPermissions> OnClientNeededPermissions;
		public event EventHandler<ClientNeededPermissions> OnEachClientNeededPermissions;
		public event NotifyEventHandler<ClientPoke> OnClientPoke;
		public event EventHandler<ClientPoke> OnEachClientPoke;
		public event NotifyEventHandler<ClientServerGroup> OnClientServerGroup;
		public event EventHandler<ClientServerGroup> OnEachClientServerGroup;
		public event NotifyEventHandler<ClientServerGroupAdded> OnClientServerGroupAdded;
		public event EventHandler<ClientServerGroupAdded> OnEachClientServerGroupAdded;
		public event NotifyEventHandler<CommandError> OnCommandError;
		public event EventHandler<CommandError> OnEachCommandError;
		public event NotifyEventHandler<ConnectionInfo> OnConnectionInfo;
		public event EventHandler<ConnectionInfo> OnEachConnectionInfo;
		public event NotifyEventHandler<ConnectionInfoRequest> OnConnectionInfoRequest;
		public event EventHandler<ConnectionInfoRequest> OnEachConnectionInfoRequest;
		public event NotifyEventHandler<FileDownload> OnFileDownload;
		public event EventHandler<FileDownload> OnEachFileDownload;
		public event NotifyEventHandler<FileInfoTs> OnFileInfoTs;
		public event EventHandler<FileInfoTs> OnEachFileInfoTs;
		public event NotifyEventHandler<FileList> OnFileList;
		public event EventHandler<FileList> OnEachFileList;
		public event NotifyEventHandler<FileListFinished> OnFileListFinished;
		public event EventHandler<FileListFinished> OnEachFileListFinished;
		public event NotifyEventHandler<FileTransfer> OnFileTransfer;
		public event EventHandler<FileTransfer> OnEachFileTransfer;
		public event NotifyEventHandler<FileTransferStatus> OnFileTransferStatus;
		public event EventHandler<FileTransferStatus> OnEachFileTransferStatus;
		public event NotifyEventHandler<FileUpload> OnFileUpload;
		public event EventHandler<FileUpload> OnEachFileUpload;
		public event NotifyEventHandler<InitIvExpand> OnInitIvExpand;
		public event EventHandler<InitIvExpand> OnEachInitIvExpand;
		public event NotifyEventHandler<InitIvExpand2> OnInitIvExpand2;
		public event EventHandler<InitIvExpand2> OnEachInitIvExpand2;
		public event NotifyEventHandler<InitServer> OnInitServer;
		public event EventHandler<InitServer> OnEachInitServer;
		public event NotifyEventHandler<PluginCommand> OnPluginCommand;
		public event EventHandler<PluginCommand> OnEachPluginCommand;
		public event NotifyEventHandler<ServerEdited> OnServerEdited;
		public event EventHandler<ServerEdited> OnEachServerEdited;
		public event NotifyEventHandler<ServerGroupList> OnServerGroupList;
		public event EventHandler<ServerGroupList> OnEachServerGroupList;
		public override event NotifyEventHandler<TextMessage> OnTextMessage;
		public event EventHandler<TextMessage> OnEachTextMessage;
		public event NotifyEventHandler<TokenUsed> OnTokenUsed;
		public event EventHandler<TokenUsed> OnEachTokenUsed;


		private void InvokeEvent(LazyNotification lazyNotification)
		{
			var ntf = lazyNotification.Notifications;
			switch (lazyNotification.NotifyType)
			{
			
			case NotificationType.ChannelChanged: {
				var ntfc = (ChannelChanged[])ntf;
				ProcessChannelChanged(ntfc);
				OnChannelChanged?.Invoke(this, ntfc);
				var ev = OnEachChannelChanged;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachChannelChanged(that);
				}
				break;
			}
			
			case NotificationType.ChannelCreated: {
				var ntfc = (ChannelCreated[])ntf;
				ProcessChannelCreated(ntfc);
				OnChannelCreated?.Invoke(this, ntfc);
				var ev = OnEachChannelCreated;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachChannelCreated(that);
					book?.UpdateChannelCreated(that);
				}
				break;
			}
			
			case NotificationType.ChannelDeleted: {
				var ntfc = (ChannelDeleted[])ntf;
				ProcessChannelDeleted(ntfc);
				OnChannelDeleted?.Invoke(this, ntfc);
				var ev = OnEachChannelDeleted;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachChannelDeleted(that);
					book?.UpdateChannelDeleted(that);
				}
				break;
			}
			
			case NotificationType.ChannelEdited: {
				var ntfc = (ChannelEdited[])ntf;
				ProcessChannelEdited(ntfc);
				OnChannelEdited?.Invoke(this, ntfc);
				var ev = OnEachChannelEdited;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachChannelEdited(that);
					book?.UpdateChannelEdited(that);
				}
				break;
			}
			
			case NotificationType.ChannelGroupList: {
				var ntfc = (ChannelGroupList[])ntf;
				ProcessChannelGroupList(ntfc);
				OnChannelGroupList?.Invoke(this, ntfc);
				var ev = OnEachChannelGroupList;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachChannelGroupList(that);
				}
				break;
			}
			
			case NotificationType.ChannelList: {
				var ntfc = (ChannelList[])ntf;
				ProcessChannelList(ntfc);
				OnChannelList?.Invoke(this, ntfc);
				var ev = OnEachChannelList;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachChannelList(that);
					book?.UpdateChannelList(that);
				}
				break;
			}
			
			case NotificationType.ChannelListFinished: {
				var ntfc = (ChannelListFinished[])ntf;
				ProcessChannelListFinished(ntfc);
				OnChannelListFinished?.Invoke(this, ntfc);
				var ev = OnEachChannelListFinished;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachChannelListFinished(that);
				}
				break;
			}
			
			case NotificationType.ChannelMoved: {
				var ntfc = (ChannelMoved[])ntf;
				ProcessChannelMoved(ntfc);
				OnChannelMoved?.Invoke(this, ntfc);
				var ev = OnEachChannelMoved;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachChannelMoved(that);
					book?.UpdateChannelMoved(that);
				}
				break;
			}
			
			case NotificationType.ChannelPasswordChanged: {
				var ntfc = (ChannelPasswordChanged[])ntf;
				ProcessChannelPasswordChanged(ntfc);
				OnChannelPasswordChanged?.Invoke(this, ntfc);
				var ev = OnEachChannelPasswordChanged;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachChannelPasswordChanged(that);
				}
				break;
			}
			
			case NotificationType.ChannelSubscribed: {
				var ntfc = (ChannelSubscribed[])ntf;
				ProcessChannelSubscribed(ntfc);
				OnChannelSubscribed?.Invoke(this, ntfc);
				var ev = OnEachChannelSubscribed;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachChannelSubscribed(that);
				}
				break;
			}
			
			case NotificationType.ChannelUnsubscribed: {
				var ntfc = (ChannelUnsubscribed[])ntf;
				ProcessChannelUnsubscribed(ntfc);
				OnChannelUnsubscribed?.Invoke(this, ntfc);
				var ev = OnEachChannelUnsubscribed;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachChannelUnsubscribed(that);
				}
				break;
			}
			
			case NotificationType.ClientChannelGroupChanged: {
				var ntfc = (ClientChannelGroupChanged[])ntf;
				ProcessClientChannelGroupChanged(ntfc);
				OnClientChannelGroupChanged?.Invoke(this, ntfc);
				var ev = OnEachClientChannelGroupChanged;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachClientChannelGroupChanged(that);
					book?.UpdateClientChannelGroupChanged(that);
				}
				break;
			}
			
			case NotificationType.ClientChatComposing: {
				var ntfc = (ClientChatComposing[])ntf;
				ProcessClientChatComposing(ntfc);
				OnClientChatComposing?.Invoke(this, ntfc);
				var ev = OnEachClientChatComposing;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachClientChatComposing(that);
				}
				break;
			}
			
			case NotificationType.ClientDbIdFromUid: {
				var ntfc = (ClientDbIdFromUid[])ntf;
				ProcessClientDbIdFromUid(ntfc);
				OnClientDbIdFromUid?.Invoke(this, ntfc);
				var ev = OnEachClientDbIdFromUid;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachClientDbIdFromUid(that);
				}
				break;
			}
			
			case NotificationType.ClientEnterView: {
				var ntfc = (ClientEnterView[])ntf;
				ProcessClientEnterView(ntfc);
				OnClientEnterView?.Invoke(this, ntfc);
				var ev = OnEachClientEnterView;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachClientEnterView(that);
					book?.UpdateClientEnterView(that);
				}
				break;
			}
			
			case NotificationType.ClientIds: {
				var ntfc = (ClientIds[])ntf;
				ProcessClientIds(ntfc);
				OnClientIds?.Invoke(this, ntfc);
				var ev = OnEachClientIds;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachClientIds(that);
				}
				break;
			}
			
			case NotificationType.ClientLeftView: {
				var ntfc = (ClientLeftView[])ntf;
				ProcessClientLeftView(ntfc);
				OnClientLeftView?.Invoke(this, ntfc);
				var ev = OnEachClientLeftView;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachClientLeftView(that);
					book?.UpdateClientLeftView(that);
				}
				break;
			}
			
			case NotificationType.ClientMoved: {
				var ntfc = (ClientMoved[])ntf;
				ProcessClientMoved(ntfc);
				OnClientMoved?.Invoke(this, ntfc);
				var ev = OnEachClientMoved;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachClientMoved(that);
					book?.UpdateClientMoved(that);
				}
				break;
			}
			
			case NotificationType.ClientNeededPermissions: {
				var ntfc = (ClientNeededPermissions[])ntf;
				ProcessClientNeededPermissions(ntfc);
				OnClientNeededPermissions?.Invoke(this, ntfc);
				var ev = OnEachClientNeededPermissions;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachClientNeededPermissions(that);
				}
				break;
			}
			
			case NotificationType.ClientPoke: {
				var ntfc = (ClientPoke[])ntf;
				ProcessClientPoke(ntfc);
				OnClientPoke?.Invoke(this, ntfc);
				var ev = OnEachClientPoke;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachClientPoke(that);
				}
				break;
			}
			
			case NotificationType.ClientServerGroup: {
				var ntfc = (ClientServerGroup[])ntf;
				ProcessClientServerGroup(ntfc);
				OnClientServerGroup?.Invoke(this, ntfc);
				var ev = OnEachClientServerGroup;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachClientServerGroup(that);
				}
				break;
			}
			
			case NotificationType.ClientServerGroupAdded: {
				var ntfc = (ClientServerGroupAdded[])ntf;
				ProcessClientServerGroupAdded(ntfc);
				OnClientServerGroupAdded?.Invoke(this, ntfc);
				var ev = OnEachClientServerGroupAdded;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachClientServerGroupAdded(that);
					book?.UpdateClientServerGroupAdded(that);
				}
				break;
			}
			
			case NotificationType.CommandError: {
				var ntfc = (CommandError[])ntf;
				ProcessCommandError(ntfc);
				OnCommandError?.Invoke(this, ntfc);
				var ev = OnEachCommandError;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachCommandError(that);
				}
				break;
			}
			
			case NotificationType.ConnectionInfo: {
				var ntfc = (ConnectionInfo[])ntf;
				ProcessConnectionInfo(ntfc);
				OnConnectionInfo?.Invoke(this, ntfc);
				var ev = OnEachConnectionInfo;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachConnectionInfo(that);
					book?.UpdateConnectionInfo(that);
				}
				break;
			}
			
			case NotificationType.ConnectionInfoRequest: {
				var ntfc = (ConnectionInfoRequest[])ntf;
				ProcessConnectionInfoRequest(ntfc);
				OnConnectionInfoRequest?.Invoke(this, ntfc);
				var ev = OnEachConnectionInfoRequest;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachConnectionInfoRequest(that);
				}
				break;
			}
			
			case NotificationType.FileDownload: {
				var ntfc = (FileDownload[])ntf;
				ProcessFileDownload(ntfc);
				OnFileDownload?.Invoke(this, ntfc);
				var ev = OnEachFileDownload;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachFileDownload(that);
				}
				break;
			}
			
			case NotificationType.FileInfoTs: {
				var ntfc = (FileInfoTs[])ntf;
				ProcessFileInfoTs(ntfc);
				OnFileInfoTs?.Invoke(this, ntfc);
				var ev = OnEachFileInfoTs;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachFileInfoTs(that);
				}
				break;
			}
			
			case NotificationType.FileList: {
				var ntfc = (FileList[])ntf;
				ProcessFileList(ntfc);
				OnFileList?.Invoke(this, ntfc);
				var ev = OnEachFileList;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachFileList(that);
				}
				break;
			}
			
			case NotificationType.FileListFinished: {
				var ntfc = (FileListFinished[])ntf;
				ProcessFileListFinished(ntfc);
				OnFileListFinished?.Invoke(this, ntfc);
				var ev = OnEachFileListFinished;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachFileListFinished(that);
				}
				break;
			}
			
			case NotificationType.FileTransfer: {
				var ntfc = (FileTransfer[])ntf;
				ProcessFileTransfer(ntfc);
				OnFileTransfer?.Invoke(this, ntfc);
				var ev = OnEachFileTransfer;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachFileTransfer(that);
				}
				break;
			}
			
			case NotificationType.FileTransferStatus: {
				var ntfc = (FileTransferStatus[])ntf;
				ProcessFileTransferStatus(ntfc);
				OnFileTransferStatus?.Invoke(this, ntfc);
				var ev = OnEachFileTransferStatus;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachFileTransferStatus(that);
				}
				break;
			}
			
			case NotificationType.FileUpload: {
				var ntfc = (FileUpload[])ntf;
				ProcessFileUpload(ntfc);
				OnFileUpload?.Invoke(this, ntfc);
				var ev = OnEachFileUpload;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachFileUpload(that);
				}
				break;
			}
			
			case NotificationType.InitIvExpand: {
				var ntfc = (InitIvExpand[])ntf;
				ProcessInitIvExpand(ntfc);
				OnInitIvExpand?.Invoke(this, ntfc);
				var ev = OnEachInitIvExpand;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachInitIvExpand(that);
				}
				break;
			}
			
			case NotificationType.InitIvExpand2: {
				var ntfc = (InitIvExpand2[])ntf;
				ProcessInitIvExpand2(ntfc);
				OnInitIvExpand2?.Invoke(this, ntfc);
				var ev = OnEachInitIvExpand2;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachInitIvExpand2(that);
				}
				break;
			}
			
			case NotificationType.InitServer: {
				var ntfc = (InitServer[])ntf;
				ProcessInitServer(ntfc);
				OnInitServer?.Invoke(this, ntfc);
				var ev = OnEachInitServer;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachInitServer(that);
					book?.UpdateInitServer(that);
				}
				break;
			}
			
			case NotificationType.PluginCommand: {
				var ntfc = (PluginCommand[])ntf;
				ProcessPluginCommand(ntfc);
				OnPluginCommand?.Invoke(this, ntfc);
				var ev = OnEachPluginCommand;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachPluginCommand(that);
				}
				break;
			}
			
			case NotificationType.ServerEdited: {
				var ntfc = (ServerEdited[])ntf;
				ProcessServerEdited(ntfc);
				OnServerEdited?.Invoke(this, ntfc);
				var ev = OnEachServerEdited;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachServerEdited(that);
					book?.UpdateServerEdited(that);
				}
				break;
			}
			
			case NotificationType.ServerGroupList: {
				var ntfc = (ServerGroupList[])ntf;
				ProcessServerGroupList(ntfc);
				OnServerGroupList?.Invoke(this, ntfc);
				var ev = OnEachServerGroupList;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachServerGroupList(that);
					book?.UpdateServerGroupList(that);
				}
				break;
			}
			
			case NotificationType.TextMessage: {
				var ntfc = (TextMessage[])ntf;
				ProcessTextMessage(ntfc);
				OnTextMessage?.Invoke(this, ntfc);
				var ev = OnEachTextMessage;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachTextMessage(that);
				}
				break;
			}
			
			case NotificationType.TokenUsed: {
				var ntfc = (TokenUsed[])ntf;
				ProcessTokenUsed(ntfc);
				OnTokenUsed?.Invoke(this, ntfc);
				var ev = OnEachTokenUsed;
				var book = Book;
				foreach(var that in ntfc) {
					ev?.Invoke(this, that);
					ProcessEachTokenUsed(that);
				}
				break;
			}
			
			case NotificationType.Unknown:
			default:
				throw Util.UnhandledDefault(lazyNotification.NotifyType);
			}
		}

		partial void ProcessChannelChanged(ChannelChanged[] notifies);
		partial void ProcessEachChannelChanged(ChannelChanged notifies);
		partial void ProcessChannelCreated(ChannelCreated[] notifies);
		partial void ProcessEachChannelCreated(ChannelCreated notifies);
		partial void ProcessChannelDeleted(ChannelDeleted[] notifies);
		partial void ProcessEachChannelDeleted(ChannelDeleted notifies);
		partial void ProcessChannelEdited(ChannelEdited[] notifies);
		partial void ProcessEachChannelEdited(ChannelEdited notifies);
		partial void ProcessChannelGroupList(ChannelGroupList[] notifies);
		partial void ProcessEachChannelGroupList(ChannelGroupList notifies);
		partial void ProcessChannelList(ChannelList[] notifies);
		partial void ProcessEachChannelList(ChannelList notifies);
		partial void ProcessChannelListFinished(ChannelListFinished[] notifies);
		partial void ProcessEachChannelListFinished(ChannelListFinished notifies);
		partial void ProcessChannelMoved(ChannelMoved[] notifies);
		partial void ProcessEachChannelMoved(ChannelMoved notifies);
		partial void ProcessChannelPasswordChanged(ChannelPasswordChanged[] notifies);
		partial void ProcessEachChannelPasswordChanged(ChannelPasswordChanged notifies);
		partial void ProcessChannelSubscribed(ChannelSubscribed[] notifies);
		partial void ProcessEachChannelSubscribed(ChannelSubscribed notifies);
		partial void ProcessChannelUnsubscribed(ChannelUnsubscribed[] notifies);
		partial void ProcessEachChannelUnsubscribed(ChannelUnsubscribed notifies);
		partial void ProcessClientChannelGroupChanged(ClientChannelGroupChanged[] notifies);
		partial void ProcessEachClientChannelGroupChanged(ClientChannelGroupChanged notifies);
		partial void ProcessClientChatComposing(ClientChatComposing[] notifies);
		partial void ProcessEachClientChatComposing(ClientChatComposing notifies);
		partial void ProcessClientDbIdFromUid(ClientDbIdFromUid[] notifies);
		partial void ProcessEachClientDbIdFromUid(ClientDbIdFromUid notifies);
		partial void ProcessClientEnterView(ClientEnterView[] notifies);
		partial void ProcessEachClientEnterView(ClientEnterView notifies);
		partial void ProcessClientIds(ClientIds[] notifies);
		partial void ProcessEachClientIds(ClientIds notifies);
		partial void ProcessClientLeftView(ClientLeftView[] notifies);
		partial void ProcessEachClientLeftView(ClientLeftView notifies);
		partial void ProcessClientMoved(ClientMoved[] notifies);
		partial void ProcessEachClientMoved(ClientMoved notifies);
		partial void ProcessClientNeededPermissions(ClientNeededPermissions[] notifies);
		partial void ProcessEachClientNeededPermissions(ClientNeededPermissions notifies);
		partial void ProcessClientPoke(ClientPoke[] notifies);
		partial void ProcessEachClientPoke(ClientPoke notifies);
		partial void ProcessClientServerGroup(ClientServerGroup[] notifies);
		partial void ProcessEachClientServerGroup(ClientServerGroup notifies);
		partial void ProcessClientServerGroupAdded(ClientServerGroupAdded[] notifies);
		partial void ProcessEachClientServerGroupAdded(ClientServerGroupAdded notifies);
		partial void ProcessCommandError(CommandError[] notifies);
		partial void ProcessEachCommandError(CommandError notifies);
		partial void ProcessConnectionInfo(ConnectionInfo[] notifies);
		partial void ProcessEachConnectionInfo(ConnectionInfo notifies);
		partial void ProcessConnectionInfoRequest(ConnectionInfoRequest[] notifies);
		partial void ProcessEachConnectionInfoRequest(ConnectionInfoRequest notifies);
		partial void ProcessFileDownload(FileDownload[] notifies);
		partial void ProcessEachFileDownload(FileDownload notifies);
		partial void ProcessFileInfoTs(FileInfoTs[] notifies);
		partial void ProcessEachFileInfoTs(FileInfoTs notifies);
		partial void ProcessFileList(FileList[] notifies);
		partial void ProcessEachFileList(FileList notifies);
		partial void ProcessFileListFinished(FileListFinished[] notifies);
		partial void ProcessEachFileListFinished(FileListFinished notifies);
		partial void ProcessFileTransfer(FileTransfer[] notifies);
		partial void ProcessEachFileTransfer(FileTransfer notifies);
		partial void ProcessFileTransferStatus(FileTransferStatus[] notifies);
		partial void ProcessEachFileTransferStatus(FileTransferStatus notifies);
		partial void ProcessFileUpload(FileUpload[] notifies);
		partial void ProcessEachFileUpload(FileUpload notifies);
		partial void ProcessInitIvExpand(InitIvExpand[] notifies);
		partial void ProcessEachInitIvExpand(InitIvExpand notifies);
		partial void ProcessInitIvExpand2(InitIvExpand2[] notifies);
		partial void ProcessEachInitIvExpand2(InitIvExpand2 notifies);
		partial void ProcessInitServer(InitServer[] notifies);
		partial void ProcessEachInitServer(InitServer notifies);
		partial void ProcessPluginCommand(PluginCommand[] notifies);
		partial void ProcessEachPluginCommand(PluginCommand notifies);
		partial void ProcessServerEdited(ServerEdited[] notifies);
		partial void ProcessEachServerEdited(ServerEdited notifies);
		partial void ProcessServerGroupList(ServerGroupList[] notifies);
		partial void ProcessEachServerGroupList(ServerGroupList notifies);
		partial void ProcessTextMessage(TextMessage[] notifies);
		partial void ProcessEachTextMessage(TextMessage notifies);
		partial void ProcessTokenUsed(TokenUsed[] notifies);
		partial void ProcessEachTokenUsed(TokenUsed notifies);
		
	}
}