class Bot implements IPage {
    public async init() {
        Get.site("playcontrols.html").then(playCtrl => {
            const divPlayBlock = Util.getElementByIdSafe("playblock");
            divPlayBlock.innerHTML = playCtrl;
            PlayControls.enable();
        });

        // get information
        const divBotInfo = Util.getElementByIdSafe("bot_info");
        const botId = Main.state["bot_id"];
        if (!botId)
            return;
        const botInfo = await Get.api(
            cmd("bot", "use", botId,
                cmd("json", "merge",
                    cmd("bot", "info"),
                    cmd("bot", "info", "client"),
                    cmd("song"),
                    cmd("song", "position"),
                    cmd("repeat"),
                    cmd("random"),
                    cmd("volume"),
                )
            )
        );
        console.log(botInfo);
    }
}
