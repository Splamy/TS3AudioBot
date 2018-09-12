class Bots implements IPage {
    private divBots!: HTMLElement;
    private bots: { [key: string]: IPlusInfo } = {};

    public async init() {
        this.divBots = Util.getElementByIdSafe("bots");
        await this.refresh();
    }

    public async refresh() {
        // TODO
        // const res = await Get.api(jmerge(
        //     cmd<IBotInfo[]>("bot", "list"),
        //     cmd<{ [key: string]: IBotsSettings }>("settings", "global", "get", "bots"),
        // ));
        const res0 = await Get.api(cmd<IBotInfo[]>("bot", "list"));
        const res1 = await Get.api(cmd<{ [key: string]: IBotsSettings }>("settings", "global", "get", "bots"));

        Util.clearChildren(this.divBots);

        if (res0 instanceof ErrorObject)
            return console.log("Error getting bot list", res0);
        if (res1 instanceof ErrorObject)
            return console.log("Error getting bot list", res1);

        this.bots = {};

        for (const botInfo of res0) {
            let bot: IPlusInfo = botInfo as IPlusInfo;
            bot.Running = true;
            this.bots[botInfo.Name] = bot;
        }

        for (const botName in res1) {
            let bot = this.bots[botName];
            if (bot === undefined) {
                bot = this.bots[botName] = {
                    Name: botName,
                    Running: false,
                }
            }
            bot.Autostart = res1[botName].run;
        }

        for (const botInfoName in this.bots) {
            this.refreshBot(this.bots[botInfoName]);
        }
    }

    public refreshBot(botInfo: IPlusInfo) {
        const botCard = this.botCard(botInfo);
        if (botCard !== undefined) {
            let oldInfo = this.bots[botInfo.Name];
            if (oldInfo !== undefined && oldInfo.Div !== undefined) {
                const oldDiv = oldInfo.Div;
                this.divBots.replaceChild(botCard, oldDiv);
            } else {
                this.divBots.appendChild(botCard);
            }
            botInfo.Div = botCard;
        }
    }

    private botCard(botInfo: IPlusInfo): HTMLElement | undefined {
        let divStartStopButton: IJsxGet = {};
        let div = <div class={"botCard formbox" + (botInfo.Running ? " botRunning" : "")}>
            <div class="formheader flex2">
                <div>{botInfo.Name}</div>
                <div when={botInfo.Id !== undefined}>
                    [ID:{botInfo.Id}]
                </div>
            </div>
            <div class="formcontent">
                <div class="formdatablock">
                    <div>Server:</div>
                    <div>{botInfo.Server}</div>
                </div>
                <div class="flex2">
                    <div>
                        <a when={botInfo.Running} class="jslink button" href={"index.html?page=bot.html&bot_id=" + botInfo.Id}>Panel</a>
                    </div>
                    <div class="button buttonIcon" set={divStartStopButton}>{botInfo.Running ? "Stop" : "Start"}</div>
                </div>
            </div>
        </div>;

        if (divStartStopButton.element !== undefined) {
            const divSs = divStartStopButton.element;
            divSs.onclick = async (_) => {
                Util.setIcon(divSs, "cog-work");
                divSs.style.color = "transparent";
                if (!botInfo.Running) {
                    const res = await Get.api(cmd<IBotInfo>("bot", "connect", "template", botInfo.Name));
                    if (res instanceof ErrorObject) {
                        Util.clearIcon(divSs);
                        divSs.style.color = null;
                        return console.log("Error starting bot", res);
                    }
                    Object.assign(botInfo, res);
                    botInfo.Running = true;
                } else {
                    const res = await Get.api(bot(cmd("bot", "disconnect"), botInfo.Id));
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
            };
        }

        return div;
    }
}

type IPlusInfo = Partial<IBotInfo> & {
    Name: string;
    Running: boolean;
    Autostart?: boolean;
    Div?: HTMLElement;
};
