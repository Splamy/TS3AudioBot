/// <reference path="Get.ts"/>
/// <reference path="Pages/IPage.ts"/>
/// <reference path="Pages/Bot.ts"/>
/// <reference path="Pages/Bots.tsx"/>

// Python webhost:
// python -m SimpleHTTPServer 8000

class Main {
    private static contentDiv: HTMLElement;
    public static AuthData: ApiAuth = ApiAuth.Anonymous;
    private static pages: Dict<IPage> = {
        "bot.html": new Bot(),
        "bots.html": new Bots(),
    };
    public static state: Dict<string> = { };

    public static async init() {
        Main.contentDiv = Util.getElementByIdSafe("content")!;
        // Main.initPureCss();
        Main.readStateFromUrl();
        Main.generateLinks();

        const authElem = document.getElementById("authtoken");
        if (authElem) {
            authElem.oninput = Main.authChanged;
        }

        const page = Main.state.page as string | undefined;
        if (page !== undefined) {
            await Main.setSite(page);
        }
    }

    private static generateLinks() {
        const list = document.querySelectorAll(".jslink") as NodeListOf<HTMLLinkElement>;
        for (const divLink of list) {
            const query = Util.parseUrlQuery(divLink.href);
            const page = query.page as string;
            divLink.classList.remove("jslink");
            divLink.onclick = async (ev) => {
                ev.preventDefault();
                await Main.setSite(page, query);
            };
        }
    }

    private static readStateFromUrl(): void {
        const query = Util.getUrlQuery();
        Object.assign(Main.state, query);
    }

    public static async setSite(site: string, data?: Dict) {
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
        Object.assign(Main.state, data);
        Main.state.page = site;
        await Main.initContentPage();
        Main.generateLinks();
        //console.log("registered " + site);
    }

    private static async initContentPage() {
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
