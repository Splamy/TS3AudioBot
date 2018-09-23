type CmdBotInfo = {
    Id: number | null;
    Name: string | null;
    Server: string;
	Status: BotStatus;
}

type CmdSongPosition = {
    length: string;
    position: string;
}

type CmdSong = {
    title: string;
    source: string;
}

type ApiError = {
    ErrorCode: number;
    ErrorMessage: string;
    ErrorName: string;
    HelpLink?: string;
}
