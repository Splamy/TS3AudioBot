class PlayControls {

    private repeat: RepeatKind = RepeatKind.Off;
    private random: boolean = false;
    private trackPosition: number = 0;
    private trackLength: number = 0;
    private volume: number = 0;
    private muteToggleVolume: number = 0;

    private playTick: Timer;
    private initialized: boolean;
    private divPlayBlock: HTMLElement;
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

    constructor() {
        this.initialized = false;
    }

    public enable() {
        const divPlayCtrl = Util.getElementByIdSafe("playblock");
        divPlayCtrl.classList.remove("playdisabled");
    }

    public disable() {
        const divPlayCtrl = Util.getElementByIdSafe("playblock");
        divPlayCtrl.classList.add("playdisabled");
    }

    private initialize() {
        if (this.initialized)
            return;

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

        this.divRepeat.onclick = async () => {
            Util.setIcon(this.divRepeat, "cog-work");
            const res = await Get.api(bot(cmd<[void, RepeatKind]>("json", "merge",
                cmd("repeat", RepeatKind[(this.repeat + 1) % 3].toLowerCase()),
                cmd("repeat"),
            )));
            if (res instanceof ErrorObject)
                return this.showStateRepeat(this.repeat);
            this.showStateRepeat(res[1]);
        };

        this.divRandom.onclick = async () => {
            Util.setIcon(this.divRandom, "cog-work");
            const res = await Get.api(bot(cmd<[void, boolean]>("json", "merge",
                cmd("random", (!this.random) ? "on" : "off"),
                cmd("random"),
            )));
            if (res instanceof ErrorObject)
                return this.showStateRandom(this.random);
            this.showStateRandom(res[1]);
        };

        const setVolume = async (volume: number, applySlider: boolean) => {
            const res = await Get.api(bot(cmd<[void, boolean]>("json", "merge",
                cmd("volume", volume.toString()),
                cmd("volume"),
            )));
            if (res instanceof ErrorObject)
                return this.showStateVolume(this.volume, applySlider);
            this.showStateVolume(Number(res[1]), applySlider);
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
            await setVolume(Util.slider_to_volume(Number(this.divVolumeSlider.value)), false);
        }

        this.divNext.onclick = async () => {
            const res = await Get.api(bot(cmd<void>("next")));
            if (res instanceof ErrorObject)
                return;
        }

        this.divPrev.onclick = async () => {
            const res = await Get.api(bot(cmd<void>("previous")));
            if (res instanceof ErrorObject)
                return;
        }

        this.playTick = new Timer(() => {
            if (this.trackPosition < this.trackLength) {
                this.trackPosition += 1;
                this.showStatePosition(this.trackPosition);
            }
        }, 1000);
        this.playTick.start();

        this.initialized = true;
    }

    public static get(): PlayControls | undefined {
        const elem = document.getElementById("playblock");
        if (!elem)
            return undefined;

        let playCtrl: PlayControls | undefined = (elem as any).playControls;
        if (!playCtrl) {
            playCtrl = new PlayControls();
            playCtrl.divPlayBlock = elem;

            playCtrl.initialize();

            (elem as any).playControls = playCtrl;
        }
        return playCtrl;
    }

    public showStateRepeat(state: RepeatKind) {
        this.repeat = state;
        switch (state) {
            case RepeatKind.Off:
                this.divRepeat.innerText = "off";
                break;
            case RepeatKind.One:
                this.divRepeat.innerText = "one";
                break;
            case RepeatKind.All:
                this.divRepeat.innerText = "all";
                break;
            default:
                break;
        }
        Util.setIcon(this.divRepeat, "loop-square");
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

    public showStatePlaying(playing: PlayState) {
        switch (playing) {
            case PlayState.Off:
                Util.setIcon(this.divPlay, "media-stop");
                break;
            case PlayState.Playing:
                Util.setIcon(this.divPlay, "media-pause");
                break;
            case PlayState.Paused:
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
