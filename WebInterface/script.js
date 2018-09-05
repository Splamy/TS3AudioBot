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
            const divBotInfo = Util.getElementByIdSafe("bot_info");
            const botId = Main.state["bot_id"];
            if (!botId)
                return;
            const botInfo = yield Get.api(cmd("bot", "use", botId, cmd("json", "merge", cmd("bot", "info"), cmd("bot", "info", "client"), cmd("song"), cmd("song", "position"), cmd("repeat"), cmd("random"))));
            divBotInfo.innerText = JSON.stringify(botInfo);
            console.log(botInfo);
        });
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
    static initPureCss() {
        const layout = document.getElementById("layout");
        const menu = document.getElementById("menu");
        const menuLink = document.getElementById("menuLink");
        const content = document.getElementById("main");
        function toggleClass(element, className) {
            const classes = element.className.split(/\s+/);
            const length = classes.length;
            for (let i = 0; i < length; i++) {
                if (classes[i] === className) {
                    classes.splice(i, 1);
                    break;
                }
            }
            if (length === classes.length) {
                classes.push(className);
            }
            element.className = classes.join(" ");
        }
        function toggleAll(e) {
            const active = "active";
            e.preventDefault();
            toggleClass(layout, active);
            toggleClass(menu, active);
            toggleClass(menuLink, active);
        }
        menuLink.onclick = (e) => {
            toggleAll(e);
        };
        content.onclick = (e) => {
            if (menu.className.indexOf("active") !== -1) {
                toggleAll(e);
            }
        };
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
class Util {
    static parseQuery(query) {
        const search = /([^&=]+)=?([^&]*)/g;
        const decode = (s) => decodeURIComponent(s.replace(/\+/g, " "));
        const urlParams = {};
        let match = null;
        do {
            match = search.exec(query);
            if (match === null)
                break;
            urlParams[decode(match[1])] = decode(match[2]);
        } while (match !== null);
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
        return (1.0 / Util.log10(10 - val) - 1) * (Util.scale / (1.0 / Util.log10(10 - Util.slmax) - 1));
    }
    static logarithmic_to_value(val) {
        if (val < 0)
            val = 0;
        else if (val > Util.scale)
            val = Util.scale;
        return 10 - Math.pow(10, 1.0 / (val / (Util.scale / (1.0 / Util.log10(10 - Util.slmax) - 1)) + 1));
    }
    static log10(val) {
        if (Math.log10 !== undefined)
            return Math.log10(val);
        else
            return Math.log(val) / Math.LN10;
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
}
Util.slmax = 7.0;
Util.scale = 100.0;
//# sourceMappingURL=script.js.map