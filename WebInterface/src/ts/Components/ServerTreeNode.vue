<template>
	<li>
		<div class="channel-line">
			<hovercon
				v-if="has_children"
				@click="collapsed = !collapsed"
				:icon="collapsed ? 'chevron-right' : 'chevron-down'"
			/>

			<b-icon v-else icon="dummy" />

			<a
				:class="{'is-active': active, 'playing-here': playing_here, 'entry-expand': true, 'normal-cursor': true}"
				@mouseover="hover_channel = true"
				@mouseleave="hover_channel = false"
			>
				<div class="container" style="display:flex;align-items:center;">
					<b-icon v-if="channel.type == 0" :icon="node.own.Subscribed ? 'card-bulleted-outline' : 'card-bulleted-off-outline'" />
					<b-icon v-else icon="dummy" />

					<span class="entry-expand" :class="spacer_css" style="display:flex;">{{channel.name}}</span>

					<b-icon v-if="playing_here" icon="volume-high" />

					<b-dropdown aria-role="list" position="is-bottom-left">
						<hovercon slot="trigger" role="button" icon="dots-horizontal" />

						<b-dropdown-item aria-role="listitem" @click="changeChannel">
							<span>
								<b-icon icon="shoe-print" />
							</span>
							<span>Join here</span>
						</b-dropdown-item>
						<b-dropdown-item v-if="!playing_here" aria-role="listitem" @click="whisperHere">
							<span>
								<b-icon icon="volume-high" />
							</span>
							<span>Whisper here</span>
						</b-dropdown-item>
						<b-dropdown-item v-else aria-role="listitem" @click="stopWhisperHere">
							<span>
								<b-icon icon="volume-off" />
							</span>
							<span>Stop whispering here</span>
						</b-dropdown-item>
					</b-dropdown>
				</div>
			</a>
		</div>
		<ul v-if="has_children" v-show="!collapsed" class="menu-list">
			<server-tree-user v-for="user in node.user" :key="'u' + user.Id" :node="user" :meta="meta" />
			<server-tree-node
				v-for="child in node.children"
				:key="'c' + child.own.Id"
				:node="child"
				:meta="meta"
			/>
		</ul>
	</li>
</template>

<script lang="ts">
import Vue from "vue";
import {
	CmdServerTreeChannel,
	CmdServerTreeUser,
	CmdServerTreeServer,
	CmdWhisperList,
	CmdServerTree
} from "../ApiObjects";
import { IChannelBuildNode } from "./ServerTree.vue";
import ServerTreeUser from "./ServerTreeUser.vue";
import { SpacerType } from "../Model/SpacerType";
import { cmd, bot, ApiRet, Api, jmerge } from "../Api";
import { Util } from "../Util";
import EnterPasswordModal from "../Modals/EnterPasswordModal.vue";
import Hovercon from "../Components/Hovercon.vue";
import { TargetSendMode } from "../Model/TargetSendMode";

export default Vue.component("server-tree-node", {
	props: {
		node: { type: Object as () => IChannelBuildNode, required: true },
		active: { type: Boolean, required: false, default: false },
		root: { type: Boolean, required: false, default: false },
		meta: {
			type: Object as () => {
				send_mode: CmdWhisperList;
				tree: CmdServerTree;
				refresh: Function;
			},
			required: true
		}
	},
	data() {
		return {
			SpacerType,

			collapsed: false,
			hover_channel: false
		};
	},
	created() {},
	computed: {
		botId(): number {
			return Number(this.$route.params.id);
		},
		has_children(): boolean {
			return this.node.children.length > 0 || this.node.user.length > 0;
		},
		channel(): { type: SpacerType; name: string } {
			const channel = this.node.own;
			if (!this.root)
				return { type: SpacerType.None, name: channel.Name };
			const nameSpacer = /^\[(c|r|\*|)spacer[^\]]*\](.*)$/.exec(
				channel.Name
			);
			if (nameSpacer == null)
				return { type: SpacerType.None, name: channel.Name };

			let spacer = SpacerType.None;
			if (nameSpacer[1] === "*") {
				spacer = SpacerType.StarSpacer;
				nameSpacer[2] = nameSpacer[2].repeat(50 / nameSpacer[2].length);
			} else if (nameSpacer[1] === "c") {
				spacer = SpacerType.CSpacer;
			} else if (nameSpacer[1] === "r") {
				spacer = SpacerType.RSpacer;
			}
			return { type: spacer, name: nameSpacer[2] };
		},
		spacer_css(): string {
			switch (this.channel.type) {
				case SpacerType.None:
					return "";
				case SpacerType.CSpacer:
					return "spacer-center";
				case SpacerType.RSpacer:
					return "spacer-right";
				case SpacerType.StarSpacer:
					return "spacer-fill";
				default:
					throw Error();
			}
		},
		playing_here(): boolean {
			if (this.meta.send_mode.SendMode === TargetSendMode.Voice) {
				return false;
			} else if (
				this.meta.send_mode.SendMode === TargetSendMode.Whisper
			) {
				return this.meta.send_mode.WhisperChannel.includes(
					this.node.own.Id
				);
			}
			return false;
		},
		own_client(): CmdServerTreeUser {
			return this.meta.tree.Clients[this.meta.tree.OwnClient];
		}
	},
	methods: {
		async changeChannel() {
			const channel = this.node.own;
			if (channel.Id === 0 || channel.Id === this.own_client.Channel)
				return;
			if (!channel.HasPassword) {
				const res = await bot(
					cmd<void>("bot", "move", String(channel.Id)),
					this.botId
				).get();
				if (!Util.check(this, res, "Failed to move")) return;
				this.meta.refresh();
				return;
			}
			this.$buefy.modal.open({
				parent: this,
				component: EnterPasswordModal,
				hasModalCard: true,
				props: {
					callback: async (pass: string) => {
						const res = await bot(
							cmd<void>("bot", "move", String(channel.Id), pass),
							this.botId
						).get();
						if (!Util.check(this, res, "Failed to move")) return;
						this.meta.refresh();
					}
				}
			});
		},
		async whisperHere() {
			const channel = this.node.own;
			const subCmd = cmd<void>(
				"subscribe",
				"channel",
				String(channel.Id)
			);
			const isWhisper =
				this.meta.send_mode.SendMode !== TargetSendMode.Whisper;
			let res = await bot(
				isWhisper
					? (jmerge(
							cmd<void>("whisper", "subscription"),
							subCmd
					  ) as Api<void>)
					: subCmd,
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to whisper there")) return;
			this.meta.refresh();
		},
		async stopWhisperHere() {
			const channel = this.node.own;
			const res = await bot(
				cmd<void>("unsubscribe", "channel", String(channel.Id)),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to remove whisper")) return;
			this.meta.refresh();
		}
	},
	components: {
		ServerTreeUser,
		EnterPasswordModal,
		Hovercon
	}
});
</script>

<style lang="less">
.channel-line {
	display: flex;
	align-items: center;
}
.entry-expand {
	flex: 1;
}

.normal-cursor {
	cursor: default;
}

// overwrite to prevent right stacking
.menu-list li ul {
	margin-right: 0 !important;
}

.spacer-right {
	justify-content: flex-end;
}
.spacer-center {
	justify-content: center;
}
.spacer-fill {
	justify-content: center;
	overflow: hidden;
}

.playing-here {
	animation: wave 4s linear forwards infinite;
	background: linear-gradient(
		90deg,
		rgba(0, 0, 0, 0),
		rgba(0, 0, 0, 0),
		rgba(128, 255, 128, 128),
		rgba(0, 0, 0, 0),
		rgba(0, 0, 0, 0)
	);
	background-size: 500% 100%;
}

@keyframes wave {
	0% {
		background-position: 100% 50%;
	}
	100% {
		background-position: 0% 50%;
	}
}
</style>
