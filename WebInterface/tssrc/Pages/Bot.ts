class Bot implements IPage {
    public async init() {
        // get information
        const botId = Main.state["bot_id"];
        if (!botId) {
            Bot.displayLoadError("No bot id requested");
            return;
        }

        // Set up all asynchronous calls
        //[IBotInfo, string | undefined, any, RepeatKind, boolean, number]
        const promiseBotInfo = Get.api(bot(jmerge(
            /*0*/ cmd<IBotInfo>("bot", "info"),
            /*1*/ cmd<string | null>("song"),
            /*2*/ cmd<any>("song", "position"),
            /*3*/ cmd<RepeatKind>("repeat"),
            /*4*/ cmd<boolean>("random"),
            /*5*/ cmd<number>("volume"),
        ))).catch(Util.asError);

        // Initialize Play-Controls
        const playCtrl = PlayControls.get();
        if (!playCtrl)
            return Bot.displayLoadError("Could not find play-controls");
        playCtrl.enable();

        let botInfo = await promiseBotInfo;
        if (botInfo instanceof ErrorObject)
            return Bot.displayLoadError("Failed to get bot information", botInfo);
        console.log(botInfo);

        // Fill 'Info' Block
        const divTemplate = Util.getElementByIdSafe("data_template");
        const divId = Util.getElementByIdSafe("data_id");
        const divServer = Util.getElementByIdSafe("data_server");

        divTemplate.innerText = botInfo[0].Name;
        divId.innerText = botInfo[0].Id.toString();
        divServer.innerText = botInfo[0].Server;

        // Fill 'Play' Block and assign Play-Controls data
        const divNowPlaying = Util.getElementByIdSafe("data_now_playing");
        const divPlayNew = Util.getElementByIdSafe("data_play_new") as HTMLInputElement;
        const btnPlayNew = Util.getElementByIdSafe("post_play_new");

        divNowPlaying.innerText = botInfo[1] || "Nothing...";
        btnPlayNew.onclick = async () => {
            if (divPlayNew.value) {
                Util.setIcon(btnPlayNew, "cog-work");
                const res = await Get.api(cmd("bot", "use", botId, cmd("play", divPlayNew.value)));
                Util.setIcon(btnPlayNew, "media-play");
                if (res instanceof ErrorObject)
                    return;
                divPlayNew.value = "";
            }
        };
        divPlayNew.onkeypress = async (e) => {
            if (e.key === "Enter") {
                e.preventDefault();
                btnPlayNew.click();
                return false;
            }
            return true;
        }

        playCtrl.showStatePlaying(botInfo[1] ? PlayState.Playing : PlayState.Off);
        playCtrl.showStateLength(Util.parseTimeToSeconds(botInfo[2].length));
        playCtrl.showStatePosition(Util.parseTimeToSeconds(botInfo[2].position));
        playCtrl.showStateRepeat(botInfo[3]);
        playCtrl.showStateRandom(botInfo[4]);
        playCtrl.showStateVolume(botInfo[5]);
    }

    // private refreshData() {

    //     let botInfo = await promiseBotInfo;
    //     if (botInfo instanceof ErrorObject)
    //         return Bot.displayLoadError("Failed to get bot information", botInfo);
    //     console.log(botInfo);
    // }

    public static displayLoadError(msg: string, err?: ErrorObject) {
        let errorData = undefined;
        if (err)
            errorData = err.obj;
        console.log(msg, errorData);
        // add somewhere a status bar or something
    }
}
