class Bots implements IPage {
    public async init() {
        const bots = Util.getElementByIdSafe("bots");
        const list = await Get.api<any[]>(Api.call("bot", "list"));

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
    }
}
