/// <reference path="Get.ts"/>

class Main {
    private static contentDiv: HTMLElement;

    public static init(): void {
        Main.contentDiv = document.getElementById("content")!;
        Main.initEvents();

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
    }
}

window.onload = Main.init;
