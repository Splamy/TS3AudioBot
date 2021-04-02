import { BotStatus } from "./Model/BotStatus";
import { TargetSendMode } from "./Model/TargetSendMode";

export interface IVersion {
	build: string;
	platform: string;
	sign: string;
}

export interface IPassword {
	pw: string;
	hashed: false;
	autohash: false;
}

// tslint:disable: interface-name

export interface CmdBotInfo {
	Id: number | null;
	Name: string | null;
	Server: string;
	Status: BotStatus;
}

export interface CmdSong {
	Title: string;
	Source: string;
	Length: number;
	Position: number;
	Paused: boolean;
}

export interface ApiError {
	ErrorCode: number;
	ErrorMessage: string;
	ErrorName: string;
	HelpLink?: string;
}

export interface CmdServerTree {
	OwnClient: number;
	Server: CmdServerTreeServer;
	Clients: { [id: number]: CmdServerTreeUser };
	Channels: { [id: number]: CmdServerTreeChannel };
	// ...
}

export interface CmdServerTreeServer {
	Name: string;
	// ...
}

export interface CmdServerTreeUser {
	Id: number;
	Uid: string;
	Name: string;
	Channel: number;
	// ...
}

export interface CmdServerTreeChannel {
	Id: number;
	Name: string;
	Parent: number;
	Order: number;
	HasPassword: boolean;
	Subscribed: boolean;
	// ...
}

export interface CmdPlaylistInfo {
	Id: string;
	Title: string;
	SongCount: number;
	DisplayOffset: number;
}

export interface CmdPlaylist extends CmdPlaylistInfo {
	Items: PlaylistItemGetData[];
}

export interface PlaylistItemGetData {
	Link: string;
	Title: string;
	AudioType: string;
}

export interface CmdQueueInfo extends CmdPlaylist {
	PlaybackIndex: number;
}

export interface CmdWhisperList {
	SendMode: TargetSendMode;
	GroupWhisper: {
		Target: number;
		TargetId: number;
		Type: number;
	} | null;
	WhisperClients: number[];
	WhisperChannel: number[];
}

export class Empty {

	public static CmdBotInfo(): CmdBotInfo {
		return {
			Id: null,
			Name: null,
			Server: "",
			Status: BotStatus.Offline,
		};
	}

	public static CmdServerTreeChannel(): CmdServerTreeChannel {
		return {
			Id: 0,
			Name: "",
			Order: 0,
			Parent: -1,
			HasPassword: false,
			Subscribed: false,
		};
	}

	public static CmdPlaylistInfo(): CmdPlaylistInfo {
		return {
			Id: "",
			Title: "",
			SongCount: 0,
			DisplayOffset: 0,
		};
	}

	public static CmdPlaylist(): CmdPlaylist {
		return {
			...this.CmdPlaylistInfo(),
			Items: []
		};
	}

	public static CmdQueueInfo(): CmdQueueInfo {
		return {
			...this.CmdPlaylist(),
			PlaybackIndex: 0
		};
	}
}
