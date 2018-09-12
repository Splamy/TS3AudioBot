"use strict";
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : new P(function (resolve) { resolve(result.value); }).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
class Get {
    static site(site) {
        return __awaiter(this, void 0, void 0, function* () {
            const response = yield fetch(site);
            return response.text();
        });
    }
    static api(site, login = Main.AuthData) {
        return __awaiter(this, void 0, void 0, function* () {
            let requestData = {
                cache: "no-cache",
            };
            if (!login.IsAnonymous) {
                requestData.headers = {
                    "Authorization": login.getBasic(),
                };
            }
            const apiSite = "/api" + site.done();
            let response;
            try {
                response = yield fetch(apiSite, requestData);
            }
            catch (err) {
                return new ErrorObject(err);
            }
            let json;
            if (response.status === 204) {
                json = {};
            }
            else {
                try {
                    json = yield response.json();
                }
                catch (err) {
                    return new ErrorObject(err);
                }
            }
            if (!response.ok) {
                json._httpStatusCode = response.status;
                return new ErrorObject(json);
            }
            else {
                return json;
            }
        });
    }
}
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
function cmd(...params) {
    return Api.call(...params);
}
function bot(param, id = Number(Main.state["bot_id"])) {
    return Api.call("bot", "use", id.toString(), param);
}
function jmerge(...param) {
    return Api.call("json", "merge", ...param);
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
class Bot {
    init() {
        return __awaiter(this, void 0, void 0, function* () {
            const botId = Main.state["bot_id"];
            if (!botId) {
                Bot.displayLoadError("No bot id requested");
                return;
            }
            const promiseBotInfo = Get.api(bot(jmerge(cmd("bot", "info"), cmd("song"), cmd("song", "position"), cmd("repeat"), cmd("random"), cmd("volume")))).catch(Util.asError);
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
            divId.innerText = botInfo[0].Id.toString();
            divServer.innerText = botInfo[0].Server;
            const divNowPlaying = Util.getElementByIdSafe("data_now_playing");
            const divPlayNew = Util.getElementByIdSafe("data_play_new");
            const btnPlayNew = Util.getElementByIdSafe("post_play_new");
            divNowPlaying.innerText = botInfo[1] || "Nothing...";
            btnPlayNew.onclick = () => __awaiter(this, void 0, void 0, function* () {
                if (divPlayNew.value) {
                    Util.setIcon(btnPlayNew, "cog-work");
                    const res = yield Get.api(cmd("bot", "use", botId, cmd("play", divPlayNew.value)));
                    Util.setIcon(btnPlayNew, "media-play");
                    if (res instanceof ErrorObject)
                        return;
                    divPlayNew.value = "";
                }
            });
            divPlayNew.onkeypress = (e) => __awaiter(this, void 0, void 0, function* () {
                if (e.key === "Enter") {
                    e.preventDefault();
                    btnPlayNew.click();
                    return false;
                }
                return true;
            });
            playCtrl.showStatePlaying(botInfo[1] ? PlayState.Playing : PlayState.Off);
            playCtrl.showStateLength(Util.parseTimeToSeconds(botInfo[2].length));
            playCtrl.showStatePosition(Util.parseTimeToSeconds(botInfo[2].position));
            playCtrl.showStateRepeat(botInfo[3]);
            playCtrl.showStateRandom(botInfo[4]);
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
    constructor() {
        this.bots = {};
    }
    init() {
        return __awaiter(this, void 0, void 0, function* () {
            this.divBots = Util.getElementByIdSafe("bots");
            yield this.refresh();
        });
    }
    refresh() {
        return __awaiter(this, void 0, void 0, function* () {
            const res0 = yield Get.api(cmd("bot", "list"));
            const res1 = yield Get.api(cmd("settings", "global", "get", "bots"));
            Util.clearChildren(this.divBots);
            if (res0 instanceof ErrorObject)
                return console.log("Error getting bot list", res0);
            if (res1 instanceof ErrorObject)
                return console.log("Error getting bot list", res1);
            this.bots = {};
            for (const botInfo of res0) {
                let bot = botInfo;
                bot.Running = true;
                this.bots[botInfo.Name] = bot;
            }
            for (const botName in res1) {
                let bot = this.bots[botName];
                if (bot === undefined) {
                    bot = this.bots[botName] = {
                        Name: botName,
                        Running: false,
                    };
                }
                bot.Autostart = res1[botName].run;
            }
            for (const botInfoName in this.bots) {
                this.refreshBot(this.bots[botInfoName]);
            }
        });
    }
    refreshBot(botInfo) {
        const botCard = this.botCard(botInfo);
        if (botCard !== undefined) {
            let oldInfo = this.bots[botInfo.Name];
            if (oldInfo !== undefined && oldInfo.Div !== undefined) {
                const oldDiv = oldInfo.Div;
                this.divBots.replaceChild(botCard, oldDiv);
            }
            else {
                this.divBots.appendChild(botCard);
            }
            botInfo.Div = botCard;
        }
    }
    botCard(botInfo) {
        let divStartStopButton = {};
        let div = createElement("div", { class: "botCard formbox" + (botInfo.Running ? " botRunning" : "") },
            createElement("div", { class: "formheader flex2" },
                createElement("div", null, botInfo.Name),
                createElement("div", { when: botInfo.Id !== undefined },
                    "[ID:",
                    botInfo.Id,
                    "]")),
            createElement("div", { class: "formcontent" },
                createElement("div", { class: "formdatablock" },
                    createElement("div", null, "Server:"),
                    createElement("div", null, botInfo.Server)),
                createElement("div", { class: "flex2" },
                    createElement("div", null,
                        createElement("a", { when: botInfo.Running, class: "jslink button", href: "index.html?page=bot.html&bot_id=" + botInfo.Id }, "Panel")),
                    createElement("div", { class: "button buttonIcon", set: divStartStopButton }, botInfo.Running ? "Stop" : "Start"))));
        if (divStartStopButton.element !== undefined) {
            const divSs = divStartStopButton.element;
            divSs.onclick = (_) => __awaiter(this, void 0, void 0, function* () {
                Util.setIcon(divSs, "cog-work");
                divSs.style.color = "transparent";
                if (!botInfo.Running) {
                    const res = yield Get.api(cmd("bot", "connect", "template", botInfo.Name));
                    if (res instanceof ErrorObject) {
                        Util.clearIcon(divSs);
                        divSs.style.color = null;
                        return console.log("Error starting bot", res);
                    }
                    Object.assign(botInfo, res);
                    botInfo.Running = true;
                }
                else {
                    const res = yield Get.api(bot(cmd("bot", "disconnect"), botInfo.Id));
                    if (res instanceof ErrorObject) {
                        Util.clearIcon(divSs);
                        divSs.style.color = null;
                        return console.log("Error starting bot", res);
                    }
                    botInfo.Id = undefined;
                    botInfo.Server = undefined;
                    botInfo.Running = false;
                }
                this.refreshBot(botInfo);
            });
        }
        return div;
    }
}
class Main {
    static init() {
        return __awaiter(this, void 0, void 0, function* () {
            Main.contentDiv = Util.getElementByIdSafe("content");
            Main.readStateFromUrl();
            Main.generateLinks();
            const authElem = document.getElementById("authtoken");
            if (authElem) {
                authElem.oninput = Main.authChanged;
            }
            const page = Main.state.page;
            if (page !== undefined) {
                yield Main.setSite(page);
            }
        });
    }
    static generateLinks() {
        const list = document.querySelectorAll(".jslink");
        for (const divLink of list) {
            const query = Util.parseUrlQuery(divLink.href);
            const page = query.page;
            divLink.classList.remove("jslink");
            divLink.onclick = (ev) => __awaiter(this, void 0, void 0, function* () {
                ev.preventDefault();
                yield Main.setSite(page, query);
            });
        }
    }
    static readStateFromUrl() {
        const query = Util.getUrlQuery();
        Object.assign(Main.state, query);
    }
    static setSite(site, data) {
        return __awaiter(this, void 0, void 0, function* () {
            const content = yield Get.site(site);
            Main.contentDiv.innerHTML = content;
            Object.assign(Main.state, data);
            Main.state.page = site;
            yield Main.initContentPage();
            Main.generateLinks();
        });
    }
    static initContentPage() {
        return __awaiter(this, void 0, void 0, function* () {
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
window.onload = Main.init;
class PlayControls {
    constructor() {
        this.playing = PlayState.Off;
        this.repeat = RepeatKind.Off;
        this.random = false;
        this.trackPosition = 0;
        this.trackLength = 0;
        this.volume = 0;
        this.muteToggleVolume = 0;
        this.divRepeat = Util.getElementByIdSafe("playctrlrepeat");
        this.divRandom = Util.getElementByIdSafe("playctrlrandom");
        this.divPlay = Util.getElementByIdSafe("playctrlplay");
        this.divPrev = Util.getElementByIdSafe("playctrlprev");
        this.divNext = Util.getElementByIdSafe("playctrlnext");
        this.divVolumeMute = Util.getElementByIdSafe("playctrlmute");
        this.divVolumeSlider = Util.getElementByIdSafe("playctrlvolume");
        this.divPositionSlider = Util.getElementByIdSafe("playctrlposition");
        this.divPosition = Util.getElementByIdSafe("data_track_position");
        this.divLength = Util.getElementByIdSafe("data_track_length");
        this.divRepeat.onclick = () => __awaiter(this, void 0, void 0, function* () {
            Util.setIcon(this.divRepeat, "cog-work");
            const res = yield Get.api(bot(jmerge(cmd("repeat", RepeatKind[(this.repeat + 1) % 3].toLowerCase()), cmd("repeat"))));
            if (res instanceof ErrorObject)
                return this.showStateRepeat(this.repeat);
            this.showStateRepeat(res[1]);
        });
        this.divRandom.onclick = () => __awaiter(this, void 0, void 0, function* () {
            Util.setIcon(this.divRandom, "cog-work");
            const res = yield Get.api(bot(jmerge(cmd("random", (!this.random) ? "on" : "off"), cmd("random"))));
            if (res instanceof ErrorObject)
                return this.showStateRandom(this.random);
            this.showStateRandom(res[1]);
        });
        const setVolume = (volume, applySlider) => __awaiter(this, void 0, void 0, function* () {
            const res = yield Get.api(bot(jmerge(cmd("volume", volume.toString()), cmd("volume"))));
            if (res instanceof ErrorObject)
                return this.showStateVolume(this.volume, applySlider);
            this.showStateVolume(res[1], applySlider);
        });
        this.divVolumeMute.onclick = () => __awaiter(this, void 0, void 0, function* () {
            if (this.muteToggleVolume !== 0 && this.volume === 0) {
                yield setVolume(this.muteToggleVolume, true);
                this.muteToggleVolume = 0;
            }
            else {
                this.muteToggleVolume = this.volume;
                yield setVolume(0, true);
            }
        });
        this.divVolumeSlider.onchange = () => __awaiter(this, void 0, void 0, function* () {
            this.muteToggleVolume = 0;
            yield setVolume(Util.slider_to_volume(Number(this.divVolumeSlider.value)), false);
        });
        this.divNext.onclick = () => __awaiter(this, void 0, void 0, function* () {
            const res = yield Get.api(bot(cmd("next")));
            if (res instanceof ErrorObject)
                return;
        });
        this.divPrev.onclick = () => __awaiter(this, void 0, void 0, function* () {
            const res = yield Get.api(bot(cmd("previous")));
            if (res instanceof ErrorObject)
                return;
        });
        this.divPlay.onclick = () => __awaiter(this, void 0, void 0, function* () {
            switch (this.playing) {
                case PlayState.Off:
                    return;
                case PlayState.Playing:
                    let res0 = yield Get.api(bot(jmerge(cmd("stop"), cmd("song"))));
                    if (res0 instanceof ErrorObject)
                        return;
                    this.showStatePlaying(res0[1] ? PlayState.Playing : PlayState.Off);
                    break;
                case PlayState.Paused:
                    let res1 = yield Get.api(bot(jmerge(cmd("play"), cmd("song"))));
                    if (res1 instanceof ErrorObject)
                        return;
                    this.showStatePlaying(res1[1] ? PlayState.Playing : PlayState.Off);
                    break;
                default:
                    break;
            }
        });
        this.playTick = new Timer(() => {
            if (this.trackPosition < this.trackLength) {
                this.trackPosition += 1;
                this.showStatePosition(this.trackPosition);
            }
        }, 1000);
    }
    enable() {
        const divPlayCtrl = Util.getElementByIdSafe("playblock");
        divPlayCtrl.classList.remove("playdisabled");
    }
    disable() {
        const divPlayCtrl = Util.getElementByIdSafe("playblock");
        divPlayCtrl.classList.add("playdisabled");
    }
    static get() {
        const elem = document.getElementById("playblock");
        if (!elem)
            return undefined;
        let playCtrl = elem.playControls;
        if (!playCtrl) {
            playCtrl = new PlayControls();
            elem.playControls = playCtrl;
        }
        return playCtrl;
    }
    showStateRepeat(state) {
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
    showStateRandom(state) {
        this.random = state;
        Util.setIcon(this.divRandom, (state ? "random" : "random-off"));
    }
    showStateVolume(volume, applySlider = true) {
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
        this.playing = playing;
        switch (playing) {
            case PlayState.Off:
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
var RepeatKind;
(function (RepeatKind) {
    RepeatKind[RepeatKind["Off"] = 0] = "Off";
    RepeatKind[RepeatKind["One"] = 1] = "One";
    RepeatKind[RepeatKind["All"] = 2] = "All";
})(RepeatKind || (RepeatKind = {}));
var PlayState;
(function (PlayState) {
    PlayState[PlayState["Off"] = 0] = "Off";
    PlayState[PlayState["Playing"] = 1] = "Playing";
    PlayState[PlayState["Paused"] = 2] = "Paused";
})(PlayState || (PlayState = {}));
class Timer {
    constructor(func, interval) {
        this.func = func;
        this.interval = interval;
    }
    start() {
        if (this.timerId !== undefined)
            return;
        this.timerId = window.setInterval(this.func, this.interval);
    }
    stop() {
        if (this.timerId === undefined)
            return;
        window.clearInterval(this.timerId);
        this.timerId = undefined;
    }
}
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
    static slider_to_volume(val) {
        if (val < 0)
            val = 0;
        else if (val > Util.slmax)
            val = Util.slmax;
        return (1.0 / Math.log10(10 - val) - 1) * (Util.scale / (1.0 / Math.log10(10 - Util.slmax) - 1));
    }
    static volume_to_slider(val) {
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
    static clearIcon(elem) {
        elem.style.backgroundImage = "none";
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
function createElement(tag, attrs, ...children) {
    if (attrs && attrs["when"] === false)
        return undefined;
    const el = document.createElement(tag);
    if (attrs && attrs["set"])
        attrs["set"].element = el;
    for (let name in attrs) {
        if (name && attrs.hasOwnProperty(name)) {
            let value = attrs[name];
            if (name === 'className' && value !== void 0) {
                el.setAttribute('class', value.toString());
            }
            else if (value === false || value === null || value === undefined || value === true) {
                el[name] = value;
            }
            else if (typeof value === 'function') {
                el[name.toLowerCase()] = value;
            }
            else if (typeof value === 'object') {
                el.setAttribute(name, value);
            }
            else {
                el.setAttribute(name, value.toString());
            }
        }
    }
    if (children && children.length > 0) {
        appendChildren(el, children);
    }
    return el;
}
function isElement(el) {
    return !!el.nodeType;
}
function addChild(parentElement, child) {
    if (child === null || child === undefined) {
        return;
    }
    else if (Array.isArray(child)) {
        appendChildren(parentElement, child);
    }
    else if (isElement(child)) {
        parentElement.appendChild(child);
    }
    else {
        parentElement.appendChild(document.createTextNode(child.toString()));
    }
}
function appendChildren(parentElement, children) {
    children.forEach(child => addChild(parentElement, child));
}
//# sourceMappingURL=script.js.map