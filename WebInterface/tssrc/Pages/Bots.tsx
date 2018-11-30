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
	private static createBotCard?: HTMLElement;

	private getCreateCard(): HTMLElement {
		if (!Bots.createBotCard) {
			Bots.createBotCard = <div class="formbox flex flexhorizontal">
				<div class="flexmax flex flexvertical" onclick={() => this.CardCreateBot()}>
					<div class="formheader centerText">Create</div>
					<div class="flexmax imageCard" style="background-image: url(media/icons/plus.svg)"></div>
				</div>
				<div class="flexmax flex flexvertical" onclick={() => this.CardQuickConnectBot()}>
					<div class="formheader centerText">Connect</div>
					<div class="flexmax imageCard" style="background-image: url(media/icons/bolt.svg)"></div>
				</div>
			</div >
		}
		return Bots.createBotCard;
	}

	public get title() { return "Bots"; }

	public async init() {
		this.divBots = Util.getElementByIdSafe("bots");
		await this.refresh();
	}

	public async refresh() {
		const res = await cmd<CmdBotInfo[]>("bot", "list").get();

		if (!DisplayError.check(res, "Error getting bot list"))
			return;

		Util.clearChildren(this.divBots);

		this.divBots.appendChild(this.getCreateCard());

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

		const div = <div class={"botCard formbox" + statusIndicator}>
			<div class="formheader flex2">
				<div>{botInfo.Name !== null ? botInfo.Name : `(=>${botInfo.Server})`}</div>
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
						<a when={botInfo.Status === BotStatus.Connected} class="jslink button buttonMedium"
							href={"index.html?page=bot.html&bot_id=" + botInfo.Id}
							style="background-image: url(media/icons/list-rich.svg)"></a>
					</div>

					<div when={botInfo.Name === null && botInfo.Id !== null}
						class="button buttonMedium"
						style={"background-image: url(media/icons/paperclip.svg)"}
						onclick={() => this.CardSaveBot(botInfo.Id!)}>
					</div>
					<div when={botInfo.Name !== null}
						class="button buttonMedium"
						style={"background-image: url(media/icons/fork.svg)"}
						onclick={() => this.CardCopyBot(botInfo.Name!)}>
					</div>
					<div when={botInfo.Name !== null}
						class="button buttonMedium"
						style={"background-image: url(media/icons/trash.svg)"}
						onclick={() => this.CardDeleteBot(botInfo.Name!)}>
					</div>
					<div class={"button buttonRound buttonMedium " + (botInfo.Status === BotStatus.Offline ? "buttonGreen" : "buttonRed")}
						set={divStartStopButton}
						style={"background-image: url(media/icons/" + (botInfo.Status === BotStatus.Offline ? "play-circle" : "power-standby") + ".svg)"}
						onclick={(e) => this.CardStartStop(botInfo, e, div)}>
					</div>
				</div>
			</div>
		</div >

		if (oldDiv !== undefined) {
			this.divBots.replaceChild(div, oldDiv);
			oldDiv = div;
		} else {
			oldDiv = this.divBots.appendChild(div);
		}

		Main.generateLinks();
	}

	private async CardStartStop(botInfo: CmdBotInfo, e: MouseEvent, oldDiv?: HTMLElement) {
		const divSs = Util.nonNull(e.target) as HTMLElement;
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
	}

	private async CardDeleteBot(name: string) {
		await ModalBox.show("Do you really want to delete the bot?", "Delete bot: " + name, {},
			{
				text: "Yes",
				default: true,
				action: async () => {
					const res = await cmd<void>("settings", "delete", name).get();
					if (DisplayError.check(res, "Error deleting bot")) {
						this.refresh();
					}
				}
			},
			{
				text: "Abort",
			});
	}

	private async CardCopyBot(name: string) {
		await ModalBox.show("", "Copy bot: " + name, {
			inputs: { target: "Enter the target template name" }
		},
			{
				text: "Ok",
				default: true,
				action: async (i) => {
					const res = await cmd<void>("settings", "copy", name, i.target).get();
					if (DisplayError.check(res, "Error copying bot")) {
						this.refresh();
					}
				}
			},
			{
				text: "Abort",
			});
	}

	private async CardCreateBot() {
		await ModalBox.show("", "Create new bot", {
			inputs: { name: "Enter the new template name" }
		},
			{
				text: "Ok",
				default: true,
				action: async (i) => {
					const res = await cmd<void>("settings", "create", i.name).get();
					if (DisplayError.check(res, "Error creating bot")) {
						this.refresh();
					}
				}
			},
			{
				text: "Abort",
			});
	}

	private async CardQuickConnectBot() {
		await ModalBox.show("", "Create new bot", {
			inputs: { address: "Enter the ip/domain/nickname to connect to" }
		},
			{
				text: "Ok",
				default: true,
				action: async (i) => {
					const res = await cmd<void>("bot", "connect", "to", i.address).get();
					if (DisplayError.check(res, "Error connecting bot")) {
						this.refresh();
					}
				}
			},
			{
				text: "Abort",
			});
	}

	private async CardSaveBot(botId: number) {
		await ModalBox.show("", "Save bot", {
			inputs: { name: "Enter the new template name" }
		},
			{
				text: "Ok",
				default: true,
				action: async (i) => {
					const res = await bot(cmd<void>("bot", "save", i.name), botId).get();
					if (DisplayError.check(res, "Error saving bot")) {
						this.refresh();
					}
				}
			},
			{
				text: "Abort",
			});
	}

	public async close() {
		this.connectCheckTicker.stop();
	}
}

interface IBotTag {
	info?: CmdBotInfo;
}

enum BotStatus {
	Offline,
	Connecting,
	Connected,
}
