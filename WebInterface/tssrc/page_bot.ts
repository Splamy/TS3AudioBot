class Bot implements IPage {
    public async init() {
        await Get.site("playcontrols.html").then(playCtrlText => {
            const divPlayBlock = Util.getElementByIdSafe("playblock");
            divPlayBlock.innerHTML = playCtrlText;
            const playCtrl = PlayControls.get();
            if (playCtrl) {
                playCtrl.enable();
            }
        }).catch(err => {
            console.log("Error loading play controls", err);
        });

        // get information
        const botId = Main.state["bot_id"];
        if (!botId) {
            Bot.displayLoadError();
            return;
        }

        await Get.api(
            cmd("bot", "use", botId,
                cmd("json", "merge",
                    /*0*/ cmd("bot", "info"),
                    /*1*/ cmd("bot", "info", "client"),
                    /*2*/ cmd("song"),
                    /*3*/ cmd("song", "position"),
                    /*4*/ cmd("repeat"),
                    /*5*/ cmd("random"),
                    /*6*/ cmd("volume"),
                )
            )
        ).then(botInfo => {
            console.log(botInfo);

            // Fill 'Info' Block
            const divTemplate = Util.getElementByIdSafe("data_template");
            const divId = Util.getElementByIdSafe("data_id");
            const divServer = Util.getElementByIdSafe("data_server");

            divTemplate.innerText = botInfo[0].Name;
            divId.innerText = botInfo[0].Id;
            divServer.innerText = botInfo[0].Server;

            // Fill 'Play' Block
            const divNowPlaying = Util.getElementByIdSafe("data_now_playing");
            const divPlayNew = Util.getElementByIdSafe("data_play_new") as HTMLInputElement;
            const btnPlayNew = Util.getElementByIdSafe("post_play_new");

            divNowPlaying.innerText = botInfo[2] ? botInfo[2] : "Nothing...";
            btnPlayNew.onclick = async () => {
                await Get.api(cmd("bot", "use", botId, cmd("play", divPlayNew.value)));
            };
        }).catch(err => {
            console.log("Could not get bot data: " + JSON.stringify(err));
        });
    }

    public static displayLoadError(obj?: any) {
        console.log("Could not get bot data: " +
            (obj !== undefined ? JSON.stringify(obj) : "{}"));
        // add somewhere a status bar or something
    }
}
