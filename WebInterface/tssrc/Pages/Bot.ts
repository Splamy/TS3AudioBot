class Bot implements IPage {
	public async init() {
		// Set up all asynchronous calls
		let refreshPromise = this.refresh();

		// Fill 'Play' Block and assign Play-Controls data
		const divPlayNew = Util.getElementByIdSafe("data_play_new") as HTMLInputElement;
		const btnPlayNew = Util.getElementByIdSafe("post_play_new");

		btnPlayNew.onclick = async () => {
			if (divPlayNew.value) {
				Util.setIcon(btnPlayNew, "cog-work");
				const res = await bot(cmd("play", divPlayNew.value)).get();
				Util.setIcon(btnPlayNew, "media-play");
				if (!DisplayError.check(res, "Failed to start a new song"))
					return;

				const playCtrl = PlayControls.get();
				if (playCtrl !== undefined)
					playCtrl.startEcho();
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

		await refreshPromise;
	}

	public async refresh() {
		const res = await bot(jmerge(
			cmd<CmdSong | null>("song"),
			cmd<CmdSongPosition>("song", "position"),
			cmd<RepeatKind>("repeat"),
			cmd<boolean>("random"),
			cmd<number>("volume"),
			cmd<CmdBotInfo>("bot", "info"),
		)).get();

		if (!DisplayError.check(res, "Failed to get bot information"))
			return;

		// Fill 'Info' Block
		const divTemplate = Util.getElementByIdSafe("data_template");
		const divId = Util.getElementByIdSafe("data_id");
		const divServer = Util.getElementByIdSafe("data_server");

		let botInfo = res[5];
		divTemplate.innerText = botInfo.Name === null ? "<temporary>" : botInfo.Name;
		divId.innerText = botInfo.Id + "";
		divServer.innerText = botInfo.Server;

		// Fill all control elements
		const playCtrl = PlayControls.get();
		if (playCtrl === undefined)
			throw new Error("Could not find play-controls");

		playCtrl.showState(res as any /*TODO:iter*/);
	}
}
