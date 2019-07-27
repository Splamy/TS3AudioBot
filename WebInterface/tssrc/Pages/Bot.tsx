class Bot implements IPage {
	public async init() {
		// Set up all asynchronous calls
		const refreshPromise = this.refresh();

		// Fill 'Play' Block and assign Play-Controls data
		const divPlayNew = Util.getElementByIdSafe("data_play_new") as HTMLInputElement;
		const btnPlayNew = Util.getElementByIdSafe("post_play_new");

		btnPlayNew.onclick = async () => {
			if (divPlayNew.value.length > 0) {
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
		};

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
			cmd<CmdServerTreeServer>("server", "tree"),
		)).get();

		if (!DisplayError.check(res, "Failed to get bot information"))
			return;

		// Fill 'Info' Block
		const divTemplate = Util.getElementByIdSafe("data_template");
		const divId = Util.getElementByIdSafe("data_id");
		const divServer = Util.getElementByIdSafe("data_server");

		const botInfo = res[5];
		divTemplate.innerText = botInfo.Name == undefined ? "<temporary>" : botInfo.Name;
		divId.innerText = String(botInfo.Id);
		divServer.innerText = botInfo.Server;

		// Fill server tree
		this.setServerTree(res[6]);

		// Fill all control elements
		const playCtrl = PlayControls.get();
		if (playCtrl === undefined)
			throw new Error("Could not find play-controls");

		playCtrl.showState(res as any /*TODO:iter*/);
	}

	public setServerTree(tree: CmdServerTreeServer) {
		const divTree = Util.getElementByIdSafe("server_tree");

		tree.Channels[0] = { Id: 0, Name: tree.Name, Order: 0, Parent: -1, HasPassword: false };

		for (const chan in tree.Channels) {
			const chanV: any = tree.Channels[chan];
			chanV.Children = [];
			chanV.User = [];
		}
		for (const chan in tree.Channels) {
			const chanV = tree.Channels[chan];
			if (chanV.Parent !== -1) {
				(tree.Channels[chanV.Parent] as any).Children.push(chanV);
			}
		}
		for (const client in tree.Clients) {
			const clientV = tree.Clients[client];
			(tree.Channels[clientV.Channel] as any).User.push(clientV);
		}

		const genTree = this.createTreeChannel(tree, tree.Channels[0]);
		genTree.classList.add("channel_root");

		Util.clearChildren(divTree);
		divTree.appendChild(genTree);
	}

	private createTreeChannel(tree: CmdServerTreeServer, channel: CmdServerTreeChannel): HTMLElement {
		const nameSpacer = /^\[(c|r|\*|)spacer[\w\d]*\](.*)$/.exec(channel.Name);
		let spacer = "";
		if (nameSpacer != undefined) {
			if (nameSpacer[1] === "*") {
				spacer = " spacer spacer_fill";
				nameSpacer[2] = nameSpacer[2].repeat(50 / nameSpacer[2].length);
			} else if (nameSpacer[1] === "c") {
				spacer = " spacer spacer_center";
			} else if (nameSpacer[1] === "r") {
				spacer = " spacer spacer_right";
			}
			channel.Name = nameSpacer[2];
		}

		return <div class="channel">
			<div class={"channel_content" + spacer} onclick={() => this.changeChannel(channel)}>
				<div class="channel_img"></div>
				<div class="channel_id">{channel.Id}</div>
				<div class="channel_name">{channel.Name}</div>
			</div>
			<div class="channel_user">
				{(channel as any).User.map((child: CmdServerTreeUser) => Bot.createTreeUser(child))}
			</div>
			<div class="channel_children">
				{(channel as any).Children.map((child: CmdServerTreeChannel) => this.createTreeChannel(tree, child))}
			</div>
		</div>;
	}

	private async changeChannel(channel: CmdServerTreeChannel) {
		if (!channel.HasPassword) {
			const res = await bot(cmd<void>("bot", "move", String(channel.Id))).get();
			if (DisplayError.check(res, "Failed to move")) {
				await this.refresh();
			}
		} else {
			await ModalBox.show("", "This channel is password protected",
				{
					inputs: { name: "Enter password" },
				},
				{
					text: "Ok",
					default: true,
					action: async (i) => {
						const res = await bot(cmd<void>("bot", "move", String(channel.Id), i.name)).get();
						if (DisplayError.check(res, "Failed to move")) {
							await this.refresh();
						}
					},
				},
				{
					text: "Abort",
				},
			);
		}
	}

	private static createTreeUser(user: CmdServerTreeUser) {
		return <div class="user user_content">
			<div class="user_img"></div>
			<div class="user_id">{user.Id}</div>
			<div class="user_name">{user.Name}</div>
		</div>;
	}
}
