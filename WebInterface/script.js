"use strict";
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : new P(function (resolve) { resolve(result.value); }).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
class Api {
    constructor(buildAddr) {
        this.buildAddr = buildAddr;
    }
    static call(...params) {
        let buildStr = "";
        for (const param of params) {
            if (typeof param === "string") {
                buildStr += "/" + encodeURIComponent(param);
            }
            else {
                buildStr += "/(" + param.done() + ")";
            }
        }
        return new Api(buildStr);
    }
    done() {
        return this.buildAddr;
    }
}
class ApiAuth {
    constructor(UserUid, Token) {
        this.UserUid = UserUid;
        this.Token = Token;
    }
    get IsAnonymous() { return this.UserUid.length === 0 && this.Token.length === 0; }
    static Create(fullTokenString) {
        if (fullTokenString.length === 0)
            return ApiAuth.Anonymous;
        const split = fullTokenString.split(/:/g);
        if (split.length === 2) {
            return new ApiAuth(split[0], split[1]);
        }
        else if (split.length === 3) {
            return new ApiAuth(split[0], split[2]);
        }
        else {
            throw new Error("Invalid token");
        }
    }
    getBasic() {
        return `Basic ${btoa(this.UserUid + ":" + this.Token)}`;
    }
}
ApiAuth.Anonymous = new ApiAuth("", "");
class Get {
    static site(site) {
        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            xhr.open("GET", site, true);
            xhr.setRequestHeader("X-Requested-With", "XMLHttpRequest");
            xhr.onload = (_) => {
                if (xhr.status >= 200 && xhr.status < 300) {
                    resolve(xhr.responseText);
                }
                else {
                    reject(xhr.responseText);
                }
            };
            xhr.onerror = (_) => {
                reject(xhr.responseText);
            };
            xhr.send();
        });
    }
    static api(site, login = Main.AuthData) {
        if (site instanceof Api) {
            site = site.done();
        }
        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            if (!login.IsAnonymous) {
                xhr.setRequestHeader("Authorization", login.getBasic());
            }
            const apiSite = "/api" + site;
            xhr.open("GET", apiSite);
            xhr.onload = (_) => {
                if (xhr.status >= 200 && xhr.status < 300) {
                    resolve(JSON.parse(xhr.responseText));
                }
                else {
                    const error = JSON.parse(xhr.responseText);
                    error.statusCode = xhr.status;
                    reject(error);
                }
            };
            xhr.onerror = (_) => {
                const error = JSON.parse(xhr.responseText);
                error.statusCode = xhr.status;
                reject(error);
            };
            xhr.send();
        });
    }
}
class Bot {
    init() {
        return __awaiter(this, void 0, void 0, function* () {
            const botId = Main.state["bot_id"];
            if (!botId) {
                Bot.displayLoadError("No bot id requested");
                return;
            }
            const promiseBotInfo = Get.api(cmd("bot", "use", botId, cmd("json", "merge", cmd("bot", "info"), cmd("song"), cmd("song", "position"), cmd("repeat"), cmd("random"), cmd("volume")))).catch(Util.asError);
            const playCtrl = PlayControls.get();
            if (!playCtrl)
                return Bot.displayLoadError("Could not find play-controls");
            playCtrl.enable();
            let botInfo = yield promiseBotInfo;
            if (botInfo instanceof ErrorObject)
                return Bot.displayLoadError("Failed to get bot information", botInfo);
            console.log(botInfo);
            const divTemplate = Util.getElementByIdSafe("data_template");
            const divId = Util.getElementByIdSafe("data_id");
            const divServer = Util.getElementByIdSafe("data_server");
            divTemplate.innerText = botInfo[0].Name;
            divId.innerText = botInfo[0].Id;
            divServer.innerText = botInfo[0].Server;
            const divNowPlaying = Util.getElementByIdSafe("data_now_playing");
            const divPlayNew = Util.getElementByIdSafe("data_play_new");
            const btnPlayNew = Util.getElementByIdSafe("post_play_new");
            divNowPlaying.innerText = botInfo[1] || "Nothing...";
            btnPlayNew.onclick = () => __awaiter(this, void 0, void 0, function* () {
                if (divPlayNew.value)
                    yield Get.api(cmd("bot", "use", botId, cmd("play", divPlayNew.value)));
            });
            playCtrl.showStateLength(Util.parseTimeToSeconds(botInfo[2].length));
            playCtrl.showStatePosition(Util.parseTimeToSeconds(botInfo[2].position));
            playCtrl.showStateVolume(botInfo[5]);
        });
    }
    static displayLoadError(msg, err) {
        let errorData = undefined;
        if (err)
            errorData = err.obj;
        console.log(msg, errorData);
    }
}
class Bots {
    init() {
        return __awaiter(this, void 0, void 0, function* () {
            const bots = Util.getElementByIdSafe("bots");
            const list = yield Get.api(Api.call("bot", "list"));
            Util.clearChildren(bots);
            for (const botInfo of list) {
                bots.innerHTML +=
                    `<li>
                    <div>${botInfo.Id}</div>
                    <div>${botInfo.Name}</div>
                    <div>${botInfo.Server}</div>
                    <div><a href="index.html?page=bot.html&bot_id=${botInfo.Id}">Go to</a></div>
                </li>`;
            }
        });
    }
}
class Main {
    static init() {
        return __awaiter(this, void 0, void 0, function* () {
            Main.contentDiv = Util.getElementByIdSafe("content");
            Main.readStateFromUrl();
            Main.initEvents();
            const page = Main.state.page;
            if (page !== undefined) {
                yield Main.setSite(page);
            }
        });
    }
    static initEvents() {
        const list = document.querySelectorAll("nav a");
        for (const link of list) {
            const query = Util.parseUrlQuery(link.href);
            const page = query.page;
            link.onclick = (ev) => __awaiter(this, void 0, void 0, function* () {
                ev.preventDefault();
                yield Main.setSite(page);
            });
        }
    }
    static readStateFromUrl() {
        const currentSite = window.location.href;
        const query = Util.parseUrlQuery(currentSite);
        for (const key in query) {
            Main.state[key] = query[key];
        }
    }
    static setSite(site) {
        return __awaiter(this, void 0, void 0, function* () {
            const content = yield Get.site(site);
            Main.contentDiv.innerHTML = content;
            Main.state.page = site;
            yield Main.registerHooks();
        });
    }
    static registerHooks() {
        return __awaiter(this, void 0, void 0, function* () {
            const authElem = document.getElementById("authtoken");
            if (authElem) {
                authElem.oninput = Main.authChanged;
            }
            const page = Main.state.page;
            if (page !== undefined) {
                const thispage = Main.pages[page];
                if (thispage !== undefined) {
                    yield thispage.init();
                }
            }
        });
    }
    static authChanged(ev) {
        const thisinput = this;
        Main.AuthData = ApiAuth.Create(thisinput.value);
    }
}
Main.AuthData = ApiAuth.Anonymous;
Main.pages = {
    "bot.html": new Bot(),
    "bots.html": new Bots(),
};
Main.state = {};
function cmd(...params) {
    return Api.call(...params);
}
window.onload = Main.init;
class PlayControls {
    constructor() {
        this.initialized = false;
    }
    enable() {
        const divPlayCtrl = Util.getElementByIdSafe("playblock");
        divPlayCtrl.classList.remove("playdisabled");
    }
    disable() {
        const divPlayCtrl = Util.getElementByIdSafe("playblock");
        divPlayCtrl.classList.add("playdisabled");
    }
    initialize() {
        if (this.initialized)
            return;
        this.divLoop = Util.getElementByIdSafe("playctrlloop");
        this.divRandom = Util.getElementByIdSafe("playctrlrandom");
        this.divPlay = Util.getElementByIdSafe("playctrlplay");
        this.divPrev = Util.getElementByIdSafe("playctrlprev");
        this.divNext = Util.getElementByIdSafe("playctrlnext");
        this.divVolumeMute = Util.getElementByIdSafe("playctrlmute");
        this.divVolumeSlider = Util.getElementByIdSafe("playctrlvolume");
        this.divPositionSlider = Util.getElementByIdSafe("playctrlposition");
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
    static get() {
        const elem = document.getElementById("playblock");
        if (!elem)
            return undefined;
        let playCtrl = elem.playControls;
        if (!playCtrl) {
            playCtrl = new PlayControls();
            playCtrl.divPlayBlock = elem;
            playCtrl.initialize();
            elem.playControls = playCtrl;
        }
        return playCtrl;
    }
    showStateLoop(state) {
    }
    showStateRandom(state) {
    }
    showStateVolume(volume, applySlider = true) {
        const logaVolume = Util.logarithmic_to_value(volume);
        this.showStateVolumeLoga(logaVolume, applySlider);
    }
    showStateVolumeLoga(logaVolume, applySlider = true) {
        if (applySlider)
            this.divVolumeSlider.value = logaVolume.toString();
        if (logaVolume <= 0.001)
            Util.setIcon(this.divVolumeMute, "volume-off");
        else if (logaVolume <= 7.0 / 2)
            Util.setIcon(this.divVolumeMute, "volume-low");
        else
            Util.setIcon(this.divVolumeMute, "volume-high");
    }
    showStateLength(length) {
        this.trackLength = length;
        const displayTime = Util.formatSecondsToTime(length);
        this.divLength.innerText = displayTime;
        this.divPositionSlider.max = length.toString();
    }
    showStatePosition(position) {
        this.trackPosition = position;
        const displayTime = Util.formatSecondsToTime(position);
        this.divPosition.innerText = displayTime;
        this.divPositionSlider.value = position.toString();
    }
    showStatePlaying(playing) {
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
var LoopKind;
(function (LoopKind) {
    LoopKind[LoopKind["Off"] = 0] = "Off";
    LoopKind[LoopKind["One"] = 1] = "One";
    LoopKind[LoopKind["All"] = 2] = "All";
})(LoopKind || (LoopKind = {}));
var PlayState;
(function (PlayState) {
    PlayState[PlayState["Off"] = 0] = "Off";
    PlayState[PlayState["Playing"] = 1] = "Playing";
    PlayState[PlayState["Paused"] = 2] = "Paused";
})(PlayState || (PlayState = {}));
class Util {
    static parseQuery(query) {
        const search = /([^&=]+)=?([^&]*)/g;
        const decode = (s) => decodeURIComponent(s.replace(/\+/g, " "));
        const urlParams = {};
        let match = null;
        do {
            match = search.exec(query);
            if (!match)
                break;
            urlParams[decode(match[1])] = decode(match[2]);
        } while (match);
        return urlParams;
    }
    static parseUrlQuery(url) {
        return Util.parseQuery(url.substr(url.indexOf("?") + 1));
    }
    static getUrlQuery() {
        return Util.parseUrlQuery(window.location.href);
    }
    static value_to_logarithmic(val) {
        if (val < 0)
            val = 0;
        else if (val > Util.slmax)
            val = Util.slmax;
        return (1.0 / Math.log10(10 - val) - 1) * (Util.scale / (1.0 / Math.log10(10 - Util.slmax) - 1));
    }
    static logarithmic_to_value(val) {
        if (val < 0)
            val = 0;
        else if (val > Util.scale)
            val = Util.scale;
        return 10 - Math.pow(10, 1.0 / (val / (Util.scale / (1.0 / Math.log10(10 - Util.slmax) - 1)) + 1));
    }
    static getElementByIdSafe(elementId) {
        return Util.nonNull(document.getElementById(elementId));
    }
    static nonNull(elem) {
        if (elem === null)
            throw new Error("Missing html element");
        return elem;
    }
    static clearChildren(elem) {
        while (elem.firstChild) {
            elem.removeChild(elem.firstChild);
        }
    }
    static setIcon(elem, icon) {
        elem.style.backgroundImage = `url(/media/icons/${icon}.svg)`;
    }
    static asError(err) {
        return new ErrorObject(err);
    }
    static parseTimeToSeconds(time) {
        const result = /(\d+):(\d+):(\d+)(?:\.(\d+))?/g.exec(time);
        if (result) {
            let num = 0;
            num += Number(result[1]) * 3600;
            num += Number(result[2]) * 60;
            num += Number(result[3]);
            if (result[4]) {
                num += Number(result[4]) / Math.pow(10, result[4].length);
            }
            return num;
        }
        return -1;
    }
    static formatSecondsToTime(seconds) {
        let str = "";
        const h = Math.floor(seconds / 3600);
        if (h > 0) {
            str += h.toString() + ":";
            seconds -= h * 3600;
        }
        const m = Math.floor(seconds / 60);
        str += ("00" + m).slice(-2) + ":";
        seconds -= m * 60;
        const s = Math.floor(seconds);
        str += ("00" + s).slice(-2);
        return str;
    }
}
Util.slmax = 7.0;
Util.scale = 100.0;
class ErrorObject {
    constructor(obj) {
        this.obj = obj;
    }
}
//# sourceMappingURL=script.js.map