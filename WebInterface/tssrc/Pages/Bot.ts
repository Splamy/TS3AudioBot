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
				if (res instanceof ErrorObject)
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
		const botInfo = await bot(jmerge(
			cmd<string | null>("song"),
			cmd<ISongLengths>("song", "position"),
			cmd<RepeatKind>("repeat"),
			cmd<boolean>("random"),
			cmd<number>("volume"),
			cmd<IBotInfo>("bot", "info"),
		)).get();

		if (botInfo instanceof ErrorObject)
			return Bot.displayLoadError("Failed to get bot information", botInfo);

		// Fill 'Info' Block
		const divTemplate = Util.getElementByIdSafe("data_template");
		const divId = Util.getElementByIdSafe("data_id");
		const divServer = Util.getElementByIdSafe("data_server");

		divTemplate.innerText = botInfo[5].Name;
		divId.innerText = botInfo[5].Id.toString();
		divServer.innerText = botInfo[5].Server;

		const playCtrl = PlayControls.get();
		if (!playCtrl)
			return Bot.displayLoadError("Could not find play-controls");

		playCtrl.showState(botInfo as any /*TODO:iter*/);
	}

	public static displayLoadError(msg: string, err?: ErrorObject) {
		let errorData = undefined;
		if (err)
			errorData = err.obj;
		console.log(msg, errorData);
		// add somewhere a status bar or something
	}
}
