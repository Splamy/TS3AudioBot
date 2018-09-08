class PlayControls {
    private initialized: boolean;
    private divPlayBlock: HTMLElement;
    private divLoop: HTMLElement;
    private divRandom: HTMLElement;
    private divPlay: HTMLElement;
    private divPrev: HTMLElement;
    private divNext: HTMLElement;

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

        this.divVolumeSlider = Util.getElementByIdSafe("playctrlvolume") as HTMLInputElement;
        this.divPositionSlider = Util.getElementByIdSafe("playctrlposition") as HTMLInputElement;
        this.divPosition = Util.getElementByIdSafe("data_track_position");
        this.divLength = Util.getElementByIdSafe("data_track_length");

        this.initialized = true;
    }

    public static get(): PlayControls | undefined {
        const elem = document.getElementById("playblock");
        if (!elem)
            return undefined;

        let playCtrl = (elem as any).playControls;
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

    public showStateVolume(volume: number) {

    }

    // in seconds
    public showStateLength(length: number) {

    }

    // in seconds
    public showStatePosition(position: number) {

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
