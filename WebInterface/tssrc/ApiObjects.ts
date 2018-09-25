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
