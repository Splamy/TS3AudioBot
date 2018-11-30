class PlayControls {
	private currentSong: CmdSong | null = null;
	private playing: PlayState = PlayState.Off;
	private repeat: RepeatKind = RepeatKind.Off;
	private random: boolean = false;
	private trackPosition: number = 0;
	private trackLength: number = 0;
	private volume: number = 0;
	private muteToggleVolume: number = 0;

	private playTick: Timer;
	private echoCounter: number = 0;
	private echoTick: Timer;

	private divRepeat: HTMLElement;
	private divRandom: HTMLElement;
	private divPlay: HTMLElement;
	private divPrev: HTMLElement;
	private divNext: HTMLElement;

	private divVolumeMute: HTMLElement;
	private divVolumeSlider: HTMLInputElement;
	private divPositionSlider: HTMLInputElement;
	private divPosition: HTMLElement;
	private divLength: HTMLElement;
	private divNowPlaying: HTMLElement;

	private constructor() {
		this.divRepeat = Util.getElementByIdSafe("playctrlrepeat");
		this.divRandom = Util.getElementByIdSafe("playctrlrandom");
		this.divPlay = Util.getElementByIdSafe("playctrlplay");
		this.divPrev = Util.getElementByIdSafe("playctrlprev");
		this.divNext = Util.getElementByIdSafe("playctrlnext");

		this.divVolumeMute = Util.getElementByIdSafe("playctrlmute");
		this.divVolumeSlider = Util.getElementByIdSafe("playctrlvolume") as HTMLInputElement;
		this.divPositionSlider = Util.getElementByIdSafe("playctrlposition") as HTMLInputElement;
		this.divPosition = Util.getElementByIdSafe("data_track_position");
		this.divLength = Util.getElementByIdSafe("data_track_length");
		this.divNowPlaying = Util.getElementByIdSafe("data_now_playing");

		this.divRepeat.onclick = async () => {
			Util.setIcon(this.divRepeat, "cog-work");
			const res = await bot(jmerge(
				cmd<void>("repeat", RepeatKind[(this.repeat + 1) % 3].toLowerCase()),
				cmd<RepeatKind>("repeat"),
			)).get();
			if (!DisplayError.check(res, "Failed to apply repeat mode"))
				return this.showStateRepeat(this.repeat);
			this.showStateRepeat(res[1]);
		};

		this.divRandom.onclick = async () => {
			Util.setIcon(this.divRandom, "cog-work");
			const res = await bot(jmerge(
				cmd<void>("random", (!this.random) ? "on" : "off"),
				cmd<boolean>("random"),
			)).get();
			if (!DisplayError.check(res, "Failed to apply random mode"))
				return this.showStateRandom(this.random);
			this.showStateRandom(res[1]);
		};

		const setVolume = async (volume: number, applySlider: boolean) => {
			const res = await bot(jmerge(
				cmd<void>("volume", volume.toString()),
				cmd<number>("volume"),
			)).get();
			if (!DisplayError.check(res, "Failed to apply volume"))
				return this.showStateVolume(this.volume, true);
			this.showStateVolume(res[1], applySlider);
		}
		this.divVolumeMute.onclick = async () => {
			if (this.muteToggleVolume !== 0 && this.volume === 0) {
				await setVolume(this.muteToggleVolume, true);
				this.muteToggleVolume = 0;
			} else {
				this.muteToggleVolume = this.volume;
				await setVolume(0, true);
			}
		}
		this.divVolumeSlider.onchange = async () => {
			this.muteToggleVolume = 0;
			this.divVolumeSlider.classList.add("loading");
			await setVolume(Util.slider_to_volume(Number(this.divVolumeSlider.value)), false);
			this.divVolumeSlider.classList.remove("loading");
		}

		this.divNext.onclick = async () => {
			Util.setIcon(this.divNext, "cog-work");
			const res = await bot(cmd<void>("next")).get();
			Util.setIcon(this.divNext, "media-skip-forward");
			if (!DisplayError.check(res, "Failed to skip forward"))
				return;
			this.startEcho();
		}

		this.divPrev.onclick = async () => {
			Util.setIcon(this.divPrev, "cog-work");
			const res = await bot(cmd<void>("previous")).get();
			Util.setIcon(this.divPrev, "media-skip-backward");
			if (!DisplayError.check(res, "Failed to skip backward"))
				return;
			this.startEcho();
		}

		this.divPlay.onclick = async () => {
			let songRet: ApiErr | [void, CmdSong | null];
			switch (this.playing) {
				case PlayState.Off:
					return;

				case PlayState.Playing:
					Util.setIcon(this.divPlay, "cog-work");
					songRet = await bot(jmerge(
						cmd<void>("stop"),
						cmd<CmdSong | null>("song"), // TODO update when better method
					)).get();
					break;

				case PlayState.Paused:
					Util.setIcon(this.divPlay, "cog-work");
					songRet = await bot(jmerge(
						cmd<void>("play"),
						cmd<CmdSong | null>("song"), // TODO update when better method
					)).get();
					break;

				default:
					throw new Error();
			}

			if (!DisplayError.check(songRet))
				return this.showStatePlaying(this.currentSong, this.playing);

			this.startEcho();
			this.showStatePlaying(songRet[1]);
		}

		this.divPositionSlider.onchange = async () => {
			if (this.playing === PlayState.Off)
				return;

			const wasRunning = this.playTick.isRunning;
			this.playTick.stop();
			this.divPositionSlider.classList.add("loading");
			const targetSeconds = Math.floor(Number(this.divPositionSlider.value));
			let res = await bot(
				cmd<void>("seek", targetSeconds.toString())
			).get();
			this.divPositionSlider.classList.remove("loading");

			if (!DisplayError.check(res, "Failed to seek"))
				return;

			if (wasRunning) this.playTick.start();
			this.showStatePosition(targetSeconds);
		}

		this.playTick = new Timer(() => {
			if (this.trackPosition < this.trackLength) {
				this.trackPosition += 1;
				this.showStatePosition(this.trackPosition);
			} else {
				this.playTick.stop();
				this.startEcho();
			}
		}, 1000);

		this.echoTick = new Timer(async () => {
			this.echoCounter += 1;
			if (this.echoCounter === 1 || this.echoCounter === 3 || this.echoCounter === 6) {
				await this.refresh();
			}
			if (this.echoCounter >= 6) {
				this.echoTick.stop();
			}
		}, 1000);

		this.enable();
	}

	public async refresh() {
		const botInfo = await bot(jmerge(
			cmd<CmdSong | null>("song"),
			cmd<CmdSongPosition>("song", "position"),
			cmd<RepeatKind>("repeat"),
			cmd<boolean>("random"),
			cmd<number>("volume"),
		)).get();

		if (!DisplayError.check(botInfo))
			return;

		this.showState(botInfo as any /*TODO:iter*/);
	}

	public showState(botInfo: [CmdSong | null, CmdSongPosition, RepeatKind, boolean, number]) {
		this.showStatePlaying(botInfo[0]);
		this.showStateLength(Util.parseTimeToSeconds(botInfo[1].length));
		this.showStatePosition(Util.parseTimeToSeconds(botInfo[1].position));
		this.showStateRepeat(botInfo[2]);
		this.showStateRandom(botInfo[3]);
		this.showStateVolume(botInfo[4]);
	}

	public enable() {
		const divPlayCtrl = Util.getElementByIdSafe("playblock");
		divPlayCtrl.classList.remove("playdisabled");
	}

	public disable() {
		const divPlayCtrl = Util.getElementByIdSafe("playblock");
		divPlayCtrl.classList.add("playdisabled");
	}

	public static get(): PlayControls | undefined {
		const elem = document.getElementById("playblock");
		if (!elem)
			return undefined;

		let playCtrl: PlayControls | undefined = (elem as any).playControls;
		if (!playCtrl) {
			playCtrl = new PlayControls();

			(elem as any).playControls = playCtrl;
		}
		return playCtrl;
	}

	public startEcho() {
		this.echoCounter = 0;
		this.echoTick.start();
	}

	public showStateRepeat(state: RepeatKind) {
		this.repeat = state;
		switch (state) {
			case RepeatKind.Off:
				Util.setIcon(this.divRepeat, "loop-off");
				break;
			case RepeatKind.One:
				Util.setIcon(this.divRepeat, "loop-one");
				break;
			case RepeatKind.All:
				Util.setIcon(this.divRepeat, "loop-all");
				break;
			default:
				break;
		}
	}

	public showStateRandom(state: boolean) {
		this.random = state;
		Util.setIcon(this.divRandom, (state ? "random" : "random-off"));
	}

	public showStateVolume(volume: number, applySlider: boolean = true) {
		this.volume = volume;
		const logaVolume = Util.volume_to_slider(volume);
		if (applySlider)
			this.divVolumeSlider.value = logaVolume.toString();
		if (logaVolume <= 0.001)
			Util.setIcon(this.divVolumeMute, "volume-off");
		else if (logaVolume <= 7.0 / 2)
			Util.setIcon(this.divVolumeMute, "volume-low");
		else
			Util.setIcon(this.divVolumeMute, "volume-high");
	}

	// in seconds
	public showStateLength(length: number) {
		this.trackLength = length;
		const displayTime = Util.formatSecondsToTime(length);
		this.divLength.innerText = displayTime;
		this.divPositionSlider.max = length.toString();
	}

	// in seconds
	public showStatePosition(position: number) {
		this.trackPosition = position;
		const displayTime = Util.formatSecondsToTime(position);
		this.divPosition.innerText = displayTime;
		this.divPositionSlider.value = position.toString();
	}

	public showStatePlaying(song: CmdSong | null, playing: PlayState = song ? PlayState.Playing : PlayState.Off) {
		if(song !== null) {
			this.currentSong = song;
			this.divNowPlaying.innerText = this.currentSong.title;
		} else {
			this.currentSong = null;
			this.divNowPlaying.innerText =  "Nothing...";
		}
		this.playing = playing;
		switch (playing) {
			case PlayState.Off:
				this.showStateLength(0);
				this.showStatePosition(0);
				this.playTick.stop();
				Util.setIcon(this.divPlay, "heart");
				break;
			case PlayState.Playing:
				this.playTick.start();
				Util.setIcon(this.divPlay, "media-stop");
				break;
			case PlayState.Paused:
				this.playTick.stop();
				Util.setIcon(this.divPlay, "media-play");
				break;
			default:
				break;
		}
	}
}

enum RepeatKind {
	Off = 0,
	One,
	All,
}

enum PlayState {
	Off,
	Playing,
	Paused,
}
