class PlayControls {
    private playTick: number;
    private trackPosition: number;
    private trackLength: number;
    private initialized: boolean;
    private divPlayBlock: HTMLElement;
    private divLoop: HTMLElement;
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

        this.divLoop = Util.getElementByIdSafe("playctrlloop");
        this.divRandom = Util.getElementByIdSafe("playctrlrandom");
        this.divPlay = Util.getElementByIdSafe("playctrlplay");
        this.divPrev = Util.getElementByIdSafe("playctrlprev");
        this.divNext = Util.getElementByIdSafe("playctrlnext");

        this.divVolumeMute = Util.getElementByIdSafe("playctrlmute");
        this.divVolumeSlider = Util.getElementByIdSafe("playctrlvolume") as HTMLInputElement;
        this.divPositionSlider = Util.getElementByIdSafe("playctrlposition") as HTMLInputElement;
        this.divPosition = Util.getElementByIdSafe("data_track_position");
        this.divLength = Util.getElementByIdSafe("data_track_length");

        this.divVolumeSlider.onchange = () => this.showStateVolumeLoga(Number(this.divVolumeSlider.value), false);

        this.playTick = setInterval(() => {
            if (this.trackPosition < this.trackLength) {
                this.trackPosition += 0.1;
                this.showStatePosition(this.trackPosition);
            }
        }, 100);

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

    public showStateLoop(state: boolean) {

    }

    public showStateRandom(state: LoopKind) {

    }

    public showStateVolume(volume: number, applySlider: boolean = true) {
        const logaVolume = Util.logarithmic_to_value(volume);
        this.showStateVolumeLoga(logaVolume, applySlider);
    }

    private showStateVolumeLoga(logaVolume: number, applySlider: boolean = true) {
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

enum LoopKind {
    Off,
    One,
    All,
}

enum PlayState {
    Off,
    Playing,
    Paused,
}
