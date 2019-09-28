<template>
	<div :class="{'has-playcontrols': online}" id="base-content">
		<div class="columns">
			<div class="column is-two-thirds">
				<div class="b-tabs">
					<div class="tabs is-boxed is-fullwidth">
						<ul>
							<bot-nav-item label="Server" icon="file-tree" page="r_server" :disabled="!online" />
							<bot-nav-item label="Settings" icon="settings" page="r_settings" />
							<bot-nav-item
								label="Playlists"
								icon="playlist-music"
								page="r_playlists"
								:props="{ playlist: '<none>' }"
								:disabled="!online"
							/>
							<bot-nav-item label="History [WIP]" icon="history" page disabled />
							<bot-nav-item label="Search [WIP]" icon="cloud-search" page disabled />
						</ul>
					</div>
				</div>
				<div class="container">
					<router-view />
				</div>
			</div>

			<div class="column is-one-third">
				<div class="notification is-info">
					<div class="formdatablock">
						<div>ID:</div>
						<div>{{botInfo.Id}}</div>
					</div>
					<div class="formdatablock">
						<div>Name:</div>
						<div>{{botInfo.Name}}</div>
					</div>
					<div class="formdatablock">
						<div>Server:</div>
						<div>{{botInfo.Server}}</div>
					</div>
					<div class="formdatablock">
						<div>Status:</div>
						<div>{{BotStatus[botInfo.Status]}}</div>
					</div>
				</div>

				<div class="notification">
					<div class="formheader">Play</div>
					<div class="formcontent">
						<div class="formdatablock">
							<div>Currently Playing:</div>
							<div>(song here)</div>
						</div>
						<div class="formdatablock">
							<b-input type="text" class="formdatablock_fill" placeholder="New song link" />
							<b-button type="is-success" icon-right="play" @click="startNewSong" />
						</div>
					</div>
				</div>
			</div>
		</div>

		<play-controls v-if="online" :botId="botId" />
	</div>
</template>

<script lang="ts">
import Vue from "vue";
import PlayControls from "../Components/PlayControls.vue";
import BotNavbarItem from "../Components/BotNavbarItem.vue";
import { CmdBotInfo } from "../ApiObjects";
import { bot, cmd } from "../Api";
import { Util } from "../Util";
import { BotStatus } from "../Model/BotStatus";

export default Vue.extend({
	props: {
		online: { type: Boolean, required: true }
	},
	created() {
		this.refresh();
	},
	data() {
		return {
			BotStatus,
			"test prop": "",

			botInfo: {} as CmdBotInfo
		};
	},
	computed: {
		botId(): number {
			return Number(this.$route.params.id);
		},
		botName(): string {
			return this.$route.params.name;
		},
		page(): string {
			return this.$route.path.substring(
				this.$route.path.lastIndexOf("/") + 1
			);
		},
		pageBase(): string {
			return this.$route.path.substring(
				0,
				this.$route.path.lastIndexOf("/")
			);
		}
	},
	methods: {
		async refresh() {
			if (this.online) {
				const res = await bot(
					cmd<CmdBotInfo>("bot", "info"),
					this.botId
				).get();

				if (!Util.check(this, res, "Failed to get bot information"))
					return;

				this.botInfo = res;
			} else {
				this.botInfo.Id = "N/A" as any as number;
				this.botInfo.Name = this.botName;
				this.botInfo.Status = BotStatus.Offline;
			}
		},
		startNewSong() {}
	},
	components: {
		PlayControls,
		BotNavbarItem
	}
});
</script>

<style lang="less">
// adjustment for play controls
#base-content.has-playcontrols {
	padding-bottom: 3.5rem;
}
</style>
