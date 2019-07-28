class Bot implements IPage {
	private ticker: Timer = new Timer(async () => await this.refresh(), 5000);
	private infoCollapse: Dict<boolean> = {};

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

		this.ticker.start();
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

		const nodes: IChannelBuildNode[] = [];
		nodes[0] = { own: { Id: 0, Name: tree.Name, Order: 0, Parent: -1, HasPassword: false }, children: [], user: [] };

		// Create structure
		for (const chan in tree.Channels) {
			const chanV: any = tree.Channels[chan];
			nodes[chanV.Id] = { own: chanV, children: [], user: [] };
		}
		// Create subchannel tree
		for (const chan in tree.Channels) {
			const chanV = tree.Channels[chan];
			nodes[chanV.Parent].children.push(nodes[chanV.Id]);
			nodes[chanV.Order].after = nodes[chanV.Id];
		}
		// Order children
		for (const node of nodes) {
			if (node.children.length === 0)
				continue;
			let cur = node.children.find(n => n.own.Order === 0);
			let reorder = [];
			while (cur !== undefined) {
				reorder.push(cur);
				cur = cur.after;
			}
			if (reorder.length !== node.children.length)
				console.log("Ordering error");
			else
				node.children = reorder;
		}
		// Add all users
		for (const client in tree.Clients) {
			const clientV = tree.Clients[client];
			nodes[clientV.Channel].user.push(clientV);
		}

		const genTree = this.createTreeChannel(nodes[0]);
		genTree.classList.add("channel_root");

		Util.clearChildren(divTree);
		divTree.appendChild(genTree);
	}

	private createTreeChannel(channelNode: IChannelBuildNode): HTMLElement {
		const channel = channelNode.own;
		const nameSpacer = /^\[(c|r|\*|)spacer[\w\d]*\](.*)$/.exec(channel.Name);
		const localId = "channel" + String(channel.Id);
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

		const detailNode = Bot.createDetailList(channel);
		this.applyDetailsCollapsed(detailNode, localId);

		return <div class="channel">
			<div class={"channel_content" + spacer}>
				<div class="channel_img" style="background-image: url(media/icons/folder.svg)"></div>
				<div class="channel_id">{channel.Id}</div>
				<div class="channel_name">{channel.Name}</div>
				<div class="tool_img"
					onclick={() => this.changeChannel(channel)}
					style="background-image: url(media/icons/share.svg)"></div>
				<div class="tool_img"
					onclick={(e) => this.toggleDetailsCollapsed(e, detailNode, localId)}
					style="background-image: url(media/icons/info.svg)"></div>
			</div>
			{detailNode}
			<div class="channel_user">
				{channelNode.user.map((user) => this.createTreeUser(user))}
			</div>
			<div class="channel_children">
				{channelNode.children.map((child) => this.createTreeChannel(child))}
			</div>
		</div>;
	}

	private createTreeUser(user: CmdServerTreeUser) {
		const localId = "user" + String(user.Id);

		const detailNode = Bot.createDetailList(user);
		this.applyDetailsCollapsed(detailNode, localId);

		return <div class="user">
			<div class="user_content">
				<div class="user_img" style="background-image: url(media/icons/person.svg)"></div>
				<div class="user_id">{user.Id}</div>
				<div class="user_name">{user.Name}</div>
				<div class="tool_img"
					onclick={(e) => this.toggleDetailsCollapsed(e, detailNode, localId)}
					style="background-image: url(media/icons/info.svg)"></div>
			</div>
			{detailNode}
		</div>;
	}

	private isDetailsCollapsed(localId: string): boolean {
		let val = this.infoCollapse[localId];
		if (val === undefined)
			val = true;
		return val;
	}

	private applyDetailsCollapsed(elem: HTMLElement, localId: string) {
		if (this.isDetailsCollapsed(localId)) {
			elem.classList.add("details_collapsed");
		} else {
			elem.classList.remove("details_collapsed");
		}
	}

	private toggleDetailsCollapsed(e: MouseEvent, elem: HTMLElement, localId: string) {
		const nval = !this.isDetailsCollapsed(localId);
		this.infoCollapse[localId] = nval;
		this.applyDetailsCollapsed(elem, localId);
		e.stopPropagation();
	}

	public async changeChannel(channel: CmdServerTreeChannel) {
		if (channel.Id === 0)
			return;

		if (!channel.HasPassword) {
			const res = await bot(cmd<void>("bot", "move", String(channel.Id))).get();
			if (DisplayError.check(res, "Failed to move")) {
				await this.refresh();
			}
			return;
		}

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

	private static createDetailList(obj: any): HTMLElement {
		const details = <div class="details"></div>;
		for (const key in obj) {
			let value = obj[key];
			if (value === null) {
				value = "";
			} else if (typeof value === "object") {
				value = JSON.stringify(value);
			}
			details.appendChild(
				<div class="detail_entry">
					<div class="detail_key">{key}</div>
					<div class="detail_value">{value}</div>
				</div>
			);
		}
		return details;
	}

	public async close() {
		this.ticker.stop();
	}
}

interface IChannelBuildNode {
	own: CmdServerTreeChannel;
	after?: IChannelBuildNode;
	children: IChannelBuildNode[];
	user: CmdServerTreeUser[];
}
