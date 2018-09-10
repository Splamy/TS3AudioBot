/// <reference path="Get.ts"/>
/// <reference path="IPage.ts"/>
/// <reference path="page_bot.ts"/>
/// <reference path="page_bots.ts"/>

// Python webhost:
// python -m SimpleHTTPServer 8000

class Main {
    private static contentDiv: HTMLElement;
    public static AuthData: ApiAuth = ApiAuth.Anonymous;
    private static pages: { [key: string]: IPage; } = {
        "bot.html": new Bot(),
        "bots.html": new Bots(),
    };
    public static state: { [key: string]: any; } = {};

    public static async init() {
        Main.contentDiv = Util.getElementByIdSafe("content")!;
        // Main.initPureCss();
        Main.readStateFromUrl();
        Main.initEvents();

        const page = Main.state.page as string | undefined;
        if (page !== undefined) {
            await Main.setSite(page);
        }
    }

    private static initEvents() {
        const list = document.querySelectorAll("nav a") as any as HTMLLinkElement[];
        for (const link of list) {
            const query = Util.parseUrlQuery(link.href);
            const page = query.page as string;
            link.onclick = async (ev) => {
                ev.preventDefault();
                await Main.setSite(page);
            };
        }
    }

    private static readStateFromUrl(): void {
        const currentSite = window.location.href;
        const query = Util.parseUrlQuery(currentSite);
        for (const key in query) {
            Main.state[key] = query[key];
        }
    }

    public static async setSite(site: string) {
        //console.log("calling " + site);
        const content = await Get.site(site);

        //console.log("got " + site);

        // Update url
        // let str = "http://splamy.de:50581/index.html";
        // let hasOne = false;
        // for (const dat in Main.state) {
        //     str += (hasOne ? "&" : "?") + dat + "=" + Main.state[dat];
        //     hasOne = false;
        // }
        // console.log("before push " + site);

        // window.history.replaceState({}, undefined, str);
        //location.href = str;
        //console.log("content " + site);
        Main.contentDiv.innerHTML = content;
        Main.state.page = site;
        await Main.registerHooks();
        //console.log("registered " + site);
    }

    private static async registerHooks() {
        const authElem = document.getElementById("authtoken");
        if (authElem) {
            authElem.oninput = Main.authChanged;
        }

        const page = Main.state.page as string | undefined;
        if (page !== undefined) {
            const thispage: IPage = Main.pages[page];
            if (thispage !== undefined) {
                await thispage.init();
            }
        }
    }

    private static authChanged(this: HTMLElement, ev: Event) {
        const thisinput = this as HTMLInputElement;
        Main.AuthData = ApiAuth.Create(thisinput.value);

        // todo do test auth
    }
}

window.onload = Main.init;
