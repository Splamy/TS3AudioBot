/// <reference path="Get.ts"/>

// Python webhost:
// python -m SimpleHTTPServer 8000

class Main {
    private static contentDiv: HTMLElement;
    private static authData: ApiAuth;

    public static init(): void {
        Main.contentDiv = document.getElementById("content")!;
        Main.initEvents();
        Main.registerHooks();

        const currentSite = window.location.href;
        const query = Util.parseQuery(currentSite.substr(currentSite.indexOf("?") + 1));
        const page = query.page as string | undefined;
        if (page !== undefined) {
            Get.site("/" + page, Main.setContent);
        }
    }

    private static initEvents(): void {
        const list = document.querySelectorAll("nav a") as any as HTMLLinkElement[];
        for (const link of list) {
            const query = Util.parseQuery(link.href.substr(link.href.indexOf("?") + 1));
            const page = query.page as string;
            link.onclick = (ev) => {
                ev.preventDefault();
                Get.site(page, Main.setContent);
            };
        }
    }

    public static setContent(content: string) {
        Main.contentDiv.innerHTML = content;
        Main.registerHooks();
    }

    private static registerHooks() {
        const authElem = document.getElementById("authtoken");
        if (authElem !== null) {
            authElem.oninput = Main.authChanged;
        }
    }

    private static authChanged(this: HTMLElement, ev: Event) {
        const thisinput = this as HTMLInputElement;
        const parts = thisinput.value.split(/:/g, 3);
        if (parts.length !== 3)
            return;

        Main.authData = new ApiAuth(parts[0], parts[2]);

        // todo do test auth
    }
}

window.onload = Main.init;
