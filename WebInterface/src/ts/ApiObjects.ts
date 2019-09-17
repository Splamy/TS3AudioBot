import { BotStatus } from "./Model/BotStatus";
import { TargetSendMode } from "./Model/TargetSendMode";

export interface CmdBotInfo {
	Id: number | null;
	Name: string | null;
	Server: string;
	Status: BotStatus;
}

export interface CmdSong {
	title: string;
	source: string;
	length: number;
	position: number;
	paused: boolean;
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
	// ...
}

export interface CmdPlaylistInfo {
	FileName: string;
	PlaylistName: string;
	SongCount: number;
	DisplayOffset: number;
	DisplayCount: number;
	// TODO
}

export interface CmdPlaylist extends CmdPlaylistInfo {
	Items: {
		Link: string;

	}[];
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
