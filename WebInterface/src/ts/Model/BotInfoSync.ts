import { CmdBotInfo, CmdQueueInfo, CmdSong, Empty } from "../ApiObjects";
import { BotStatus } from "./BotStatus";
import { RepeatKind } from "./RepeatKind";

export class BotInfoSync {
	public botInfo = Empty.CmdBotInfo();
	public nowPlaying = Empty.CmdQueueInfo();
	public volume = 0;
	public repeat = RepeatKind.Off;
	public shuffle = false;
	public song = null as CmdSong | null;
}
