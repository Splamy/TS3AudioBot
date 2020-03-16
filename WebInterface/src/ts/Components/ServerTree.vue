<template>
	<div class="box">
		<div v-if="node" class="menu">
			<ul class="menu-list">
				<p v-if="node" class="menu-label">{{node.own.Name}}</p>
				<server-tree-node
					v-for="child in node.children"
					:key="'c' + child.own.Id"
					:node="child"
					:meta="meta"
					root
				/>
			</ul>
		</div>
	</div>
</template>

<script lang="ts">
import Vue from "vue";
import ServerTreeNode from "./ServerTreeNode.vue";
import {
	CmdServerTreeChannel,
	CmdServerTreeUser,
	CmdServerTreeServer,
	CmdWhisperList,
	CmdServerTree,
	Empty
} from "../ApiObjects";
import { Dict, Util } from "../Util";
import { bot, cmd, jmerge } from "../Api";
import { Timer } from "../Timer";

export default Vue.component("server-tree", {
	data() {
		return {
			ticker: undefined! as Timer,
			node: undefined! as IChannelBuildNode,
			meta: {
				send_mode: {} as CmdWhisperList,
				tree: {} as CmdServerTree,
				refresh: undefined! as Function
			}
		};
	},
	created() {
		this.meta.refresh = () => {
			this.ticker.restart();
			this.refresh();
		};
		this.refresh();

		this.ticker = new Timer(async () => await this.refresh(), 5000);
		this.ticker.start();
	},
	destroyed() {
		this.ticker.stop();
	},
	methods: {
		async refresh() {
			const res = await bot(
				jmerge(
					cmd<CmdServerTree>("server", "tree"),
					cmd<CmdWhisperList>("whisper", "list")
				),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to get server tree")) {
				this.ticker.stop();
				return;
			}

			this.setServerTree(res[0]);
			this.meta = {
				tree: res[0],
				send_mode: res[1],
				refresh: this.meta.refresh
			};
		},
		setServerTree(tree: CmdServerTree) {
			const nodes: Dict<IChannelBuildNode> = {};
			nodes[0] = {
				own: (function() {
					let c = Empty.CmdServerTreeChannel();
					c.Name = tree.Server.Name;
					return c;
				})(),
				children: [],
				user: []
			};
			// Create structure
			for (const chan in tree.Channels) {
				const chanV: any = tree.Channels[chan];
				nodes[chanV.Id] = { own: chanV, children: [], user: [] };
			}
			// Create subchannel tree
			for (const chan in tree.Channels) {
				const chanV = tree.Channels[chan];
				nodes[chanV.Parent]!.children.push(nodes[chanV.Id]!);
				nodes[chanV.Order]!.after = nodes[chanV.Id];
			}
			// Order children
			for (const nodeId of Object.keys(nodes)) {
				const node: IChannelBuildNode = nodes[nodeId]!;
				if (node.children.length === 0) continue;
				let cur = node.children.find(n => n.own.Order === 0);
				const reorder = [];
				while (cur !== undefined) {
					reorder.push(cur);
					cur = cur.after;
				}
				if (reorder.length !== node.children.length)
					console.log("Ordering error");
				else node.children = reorder;
			}
			// Add all users
			for (const client in tree.Clients) {
				const clientV = tree.Clients[client];
				nodes[clientV.Channel]!.user.push(clientV);
			}
			this.node = nodes[0]!;
		}
	},
	computed: {
		botId(): number {
			return Number(this.$route.params.id);
		}
	},
	components: {
		ServerTreeNode
	}
});

export interface IChannelBuildNode {
	own: CmdServerTreeChannel;
	after?: IChannelBuildNode;
	children: IChannelBuildNode[];
	user: CmdServerTreeUser[];
}
</script>
