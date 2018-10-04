/// <reference path="Get.ts"/>
/// <reference path="Pages/IPage.ts"/>
/// <reference path="Pages/Bot.ts"/>
/// <reference path="Pages/Bots.tsx"/>
/// <reference path="Pages/Commands.tsx"/>
/// <reference path="Pages/Dummy.ts"/>
/// <reference path="Pages/Home.ts"/>

// Python webhost:
// python -m SimpleHTTPServer 8000

class Main {
    private static divAuthToken: HTMLInputElement;
    private static divContent: HTMLElement;
    public static AuthData: ApiAuth = ApiAuth.Anonymous;
    private static currentPage: IPage | undefined;
    private static pages: Dict<(IPage)> = {
        "home.html": new Home(),
        "bot.html": new Bot(),
        "bots.html": new Bots(),
        "commands.html": new Commands(),
        "playlist.html": new Dummy(),
        "history.html": new Dummy(),
    };
    public static state: Dict<string> = {};

    public static async init() {
        Main.divContent = Util.getElementByIdSafe("content")!;
        Main.generateLinks();

        Main.divAuthToken = Util.getElementByIdSafe("authtoken") as HTMLInputElement;
        Main.divAuthToken.oninput = Main.authChanged;
        Main.loadAuth();

        const divRefresh = document.getElementById("refreshContent");
        if (divRefresh) {
            divRefresh.onclick = async () => {
                if (Main.currentPage !== undefined) {
                    Util.setIcon(divRefresh, "reload-work");
                    await Main.currentPage.refresh();
                    Util.setIcon(divRefresh, "reload");
                }
            };
        }

        const list = document.querySelectorAll("nav a") as NodeListOf<HTMLLinkElement>;
        for (const divLink of list) {
            const query = Util.parseQuery(divLink.href);
            if (query.page) {
                const pageEntry = Main.pages[query.page];
                if (pageEntry) {
                    pageEntry.divNav = divLink;
                }
            }
        }

        Main.readStateFromUrl();
        // Set "main" as default if no page was specified
        Main.state.page = Main.state.page || "home.html";
        await Main.setSite(Main.state);
    }

    public static generateLinks() {
        const list = document.querySelectorAll(".jslink") as NodeListOf<HTMLLinkElement>;
        for (const divLink of list) {
            const query = Util.parseQuery(divLink.href);
            divLink.classList.remove("jslink");
            divLink.onclick = async (ev) => {
                ev.preventDefault();
                await Main.setSite(query);
            };
        }
    }

    private static readStateFromUrl(): void {
        const query = Util.getUrlQuery();
        Object.assign(Main.state, query);
    }

    public static async setSite(data: Dict<string> = Main.state) {
        const site = data.page
        if (site === undefined) {
            return;
        }

        try {
            const content = await Get.site(site);
            Main.divContent.innerHTML = content;
        } catch (ex) {
            DisplayError.push(undefined, "Failed to get page content.");
            return;
        }

        // Update state and url
        Main.state = data;
        window.history.pushState(Main.state, document.title, "index.html" + Util.buildQuery(Main.state));

        const oldPage = Main.currentPage;
        if (oldPage) {
            if (oldPage.close)
                await oldPage.close();
            if (oldPage.divNav)
                oldPage.divNav.classList.remove("navSelected");
        }

        const newPage = Main.pages[site];
        Main.currentPage = newPage;
        if (newPage !== undefined) {
            if (newPage.divNav) {
                newPage.divNav.classList.add("navSelected");
            }
            await newPage.init();
            if (newPage.title)
                document.title = "TS3AudioBot - " + newPage.title;
        }

        Main.generateLinks();
    }

    private static loadAuth() {
        const auth = window.localStorage.getItem("api_auth");
        if (auth) {
            Main.AuthData = ApiAuth.Create(auth);
            Main.divAuthToken.value = auth;
        }
    }

    private static authChanged() {
        Main.AuthData = ApiAuth.Create(Main.divAuthToken.value);
        window.localStorage.setItem("api_auth", Main.AuthData.getFullAuth());
        // todo do test auth
    }
}

window.onload = Main.init;
