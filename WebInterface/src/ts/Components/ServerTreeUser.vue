<template>
	<li>
		<div class="channel-line">
			<!-- <b-button v-if="has_children" @click="collapsed = !collapsed" type="is-text" style="border-radius:100%;">
				<b-icon :icon="collapsed ? 'chevron-right' : 'chevron-down'"></b-icon>
			</b-button>-->
			<b-icon icon="dummy" />
			<a :class="{'is-active': active, 'playing-here': playing_here, 'entry-expand': true }">
				<div class="container" style="display:flex;align-items:center;">
					<span>
						<b-icon :icon="node.Id == own_client.Id ? 'robot' : 'account'"></b-icon>
					</span>
					<span class="entry-expand">
						<edi-text
							v-if="node.Id == own_client.Id"
							:text="node.Name"
							@onedit="botRename($event)"
						/>
						<div v-else>{{node.Name}}</div>
					</span>

					<b-icon v-if="playing_here" icon="volume-high" />

					<b-dropdown v-if="node.Id == own_client.Id" aria-role="list" position="is-bottom-left">
						<hovercon slot="trigger" role="button" icon="dots-horizontal" />

						<b-dropdown-item aria-role="listitem" @click="voiceHere">
							<span>
								<b-icon icon="volume-high" />
							</span>
							<span>Voice here</span>
						</b-dropdown-item>
					</b-dropdown>
				</div>
			</a>
		</div>
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
import { TargetSendMode } from "../Model/TargetSendMode";
import { bot, cmd } from "../Api";
import { Util } from "../Util";
import EditableText from "../Components/EditableText.vue";

export default Vue.component("server-tree-user", {
	props: {
		node: { type: Object as () => CmdServerTreeUser, required: true },
		active: { type: Boolean, required: false, default: false },
		meta: {
			type: Object as () => {
				send_mode: CmdWhisperList;
				tree: CmdServerTree;
				refresh: Function;
			},
			required: true
		}
	},
	computed: {
		botId(): number {
			return Number(this.$route.params.id);
		},
		own_client(): CmdServerTreeUser {
			return this.meta.tree.Clients[this.meta.tree.OwnClient];
		},
		playing_here(): boolean {
			if (this.meta.send_mode.SendMode === TargetSendMode.Voice) {
				return this.node.Id == this.meta.tree.OwnClient;
			} else if (
				this.meta.send_mode.SendMode === TargetSendMode.Whisper
			) {
				return this.meta.send_mode.WhisperClients.includes(
					this.node.Id
				);
			}
			return false;
		}
	},
	methods: {
		async voiceHere() {
			const res = await bot(
				cmd<void>("whisper", "off"),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to change mode")) return;
			this.meta.refresh();
		},
		// Duplicated method! (BotSettings) combine somehere maybe
		async botRename(name: string) {
			const res = await bot(
				cmd<void>("bot", "name", name),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to set name")) return;
			this.meta.refresh();
		}
	},
	components: {
		EditableText
	}
});
</script>
