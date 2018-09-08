class Bot implements IPage {
    public async init() {
        // get information
        const botId = Main.state["bot_id"];
        if (!botId) {
            Bot.displayLoadError("No bot id requested");
            return;
        }

        // Set up all asynchronous calls
        const promiseBotInfo = Get.api<any[]>(
            cmd("bot", "use", botId,
                cmd("json", "merge",
                    /*0*/ cmd("bot", "info"),
                    /*1*/ cmd("song"),
                    /*2*/ cmd("song", "position"),
                    /*3*/ cmd("repeat"),
                    /*4*/ cmd("random"),
                    /*5*/ cmd("volume"),
                )
            )
        ).catch(Util.asError);

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
        divId.innerText = botInfo[0].Id;
        divServer.innerText = botInfo[0].Server;

        // Fill 'Play' Block and assign Play-Controls data
        const divNowPlaying = Util.getElementByIdSafe("data_now_playing");
        const divPlayNew = Util.getElementByIdSafe("data_play_new") as HTMLInputElement;
        const btnPlayNew = Util.getElementByIdSafe("post_play_new");

        divNowPlaying.innerText = botInfo[1] || "Nothing...";
        btnPlayNew.onclick = async () => {
            if (divPlayNew.value)
                await Get.api(cmd("bot", "use", botId, cmd("play", divPlayNew.value)));
        };

        playCtrl.showStateLength(Util.parseTimeToSeconds(botInfo[2].length));
        playCtrl.showStatePosition(Util.parseTimeToSeconds(botInfo[2].position));
        playCtrl.showStateVolume(botInfo[5]);
    }

    public static displayLoadError(msg: string, err?: ErrorObject) {
        let errorData = undefined;
        if (err)
            errorData = err.obj;
        console.log(msg, errorData);
        // add somewhere a status bar or something
    }
}
