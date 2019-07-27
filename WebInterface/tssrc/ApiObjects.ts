interface CmdBotInfo {
	Id: number | null;
	Name: string | null;
	Server: string;
	Status: BotStatus;
}

interface CmdSongPosition {
	length: string;
	position: string;
}

interface CmdSong {
	title: string;
	source: string;
}

interface ApiError {
	ErrorCode: number;
	ErrorMessage: string;
	ErrorName: string;
	HelpLink?: string;
}

interface CmdServerTreeServer {
	Name: string;
	Clients: { [id: number]: CmdServerTreeUser };
	Channels: { [id: number]: CmdServerTreeChannel };
	// ...
}

interface CmdServerTreeUser {
	Id: number;
	Uid: string;
	Name: string;
	Channel: number;
	// ...
}

interface CmdServerTreeChannel {
	Id: number;
	Name: string;
	Parent: number;
	Order: number;
	HasPassword: boolean;
	// ...
}
