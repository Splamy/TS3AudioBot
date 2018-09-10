class Bots implements IPage {
    public async init() {
        const bots = Util.getElementByIdSafe("bots");
        Util.clearChildren(bots);

        const list = await Get.api<any[]>(cmd("bot", "list"));
        if (list instanceof ErrorObject)
            return console.log("Error getting bot list", list);

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
