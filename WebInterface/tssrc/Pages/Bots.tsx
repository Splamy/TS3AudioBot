class Bots implements IPage {
	private divBots!: HTMLElement;
	private hasConnectingBots: boolean = false;
	private readonly connectCheckTicker = new Timer(async () => {
		if (!this.hasConnectingBots) {
			this.connectCheckTicker.stop();
			return;
		}
		await this.refresh();
		if (!this.hasConnectingBots)
			this.connectCheckTicker.stop();
	}, 1000);

	public async init() {
		this.divBots = Util.getElementByIdSafe("bots");
		await this.refresh();
	}

	public async refresh() {
		const res = await cmd<CmdBotInfo[]>("bot", "list").get();

		if (!DisplayError.check(res, "Error getting bot list"))
			return;

		Util.clearChildren(this.divBots);

		res.sort((a, b) => {
			if (a.Name === null) {
				if (b.Name === null)
					return 0;
				return 1;
			} else if (b.Name === null) {
				return -1;
			}
			return a.Name.localeCompare(b.Name);
		});

		this.hasConnectingBots = false;
		for (const botInfo of res) {
			if (botInfo.Status === BotStatus.Connecting)
				this.hasConnectingBots = true;
			this.showBotCard(botInfo);
		}

		if (this.hasConnectingBots)
			this.connectCheckTicker.start();
	}

	private showBotCard(botInfo: CmdBotInfo, oldDiv?: HTMLElement) {
		let divStartStopButton: IJsxGet = {};
		const statusIndicator = botInfo.Status === BotStatus.Connected ? " botConnected"
			: botInfo.Status === BotStatus.Connecting ? " botConnecting" : "";

		let div = <div class={"botCard formbox" + statusIndicator}>
			<div class="formheader flex2">
				<div>{botInfo.Name}</div>
				<div when={botInfo.Id !== null}>
					[ID:{botInfo.Id}]
				</div>
			</div>
			<div class="formcontent">
				<div class="formdatablock">
					<div>Server:</div>
					<div>{botInfo.Server}</div>
				</div>
				<div class="formdatablock">
					<div>Status:</div>
					<div class="statusName">{BotStatus[botInfo.Status]}</div>
				</div>
				<div class="flex2">
					<div>
						<a when={botInfo.Status === BotStatus.Connected} class="jslink button buttonMedium buttonIcon"
							href={"index.html?page=bot.html&bot_id=" + botInfo.Id}
							style="background-image: url(media/icons/list-rich.svg)"></a>
					</div>
					<div class={"button buttonRound buttonMedium buttonIcon " + (botInfo.Status === BotStatus.Connected ? "buttonRed" : "buttonGreen")}
						set={divStartStopButton}
						style={"background-image: url(media/icons/" + (botInfo.Status === BotStatus.Connected ? "power-standby" : "play-circle") + ".svg)"}>
					</div>
				</div>
			</div>
		</div>

		if (oldDiv !== undefined) {
			this.divBots.replaceChild(div, oldDiv);
			oldDiv = div;
		} else {
			oldDiv = this.divBots.appendChild(div);
		}

		if (divStartStopButton.element === undefined)
			throw new Error("Bot card was built wrong");

		const divSs = divStartStopButton.element;
		divSs.onclick = async (_) => {
			Util.setIcon(divSs, "cog-work");
			if (botInfo.Status === BotStatus.Offline) {
				if (botInfo.Name === null)
					return;
				const tmpBotName = botInfo.Name;
				botInfo.Name = null;
				const res = await cmd<CmdBotInfo>("bot", "connect", "template", tmpBotName).get();
				if (!DisplayError.check(res, "Error starting bot")) {
					botInfo.Name = tmpBotName;
					Util.setIcon(divSs, "play-circle");
					return;
				}
				Object.assign(botInfo, res);
				this.hasConnectingBots = true;
				this.connectCheckTicker.start();
			} else {
				if (botInfo.Id === null)
					return;
				const tmpBotId = botInfo.Id;
				botInfo.Id = null;
				const res = await bot(cmd<void>("bot", "disconnect"), tmpBotId).get();
				if (!DisplayError.check(res, "Error stopping bot")) {
					botInfo.Id = tmpBotId;
					Util.setIcon(divSs, "power-standby");
					return;
				}
				botInfo.Id = null;
				botInfo.Status = BotStatus.Offline;
			}
			this.showBotCard(botInfo, oldDiv);
		};

		Main.generateLinks();
	}

	public async close() {
		this.connectCheckTicker.stop();
	}
}

enum BotStatus {
	Offline,
	Connecting,
	Connected,
}
