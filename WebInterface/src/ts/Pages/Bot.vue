<template>
	<div :class="{'has-playcontrols': online}" id="base-content">
		<div class="columns">
			<div class="column is-two-thirds">
				<div class="b-tabs">
					<div class="tabs is-boxed is-fullwidth">
						<ul>
							<bot-nav-item label="Server" icon="file-tree" page="r_server" :disabled="!online" />
							<bot-nav-item label="Settings" icon="cog" page="r_settings" />
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
					<router-view @requestRefresh="refresh" />
				</div>
			</div>

			<div class="column is-one-third">
				<div class="notification is-info">
					<div class="formdatablock">
						<div>ID:</div>
						<div>{{info.botInfo.Id}}</div>
					</div>
					<div class="formdatablock">
						<div>Name:</div>
						<div>{{info.botInfo.Name}}</div>
					</div>
					<div class="formdatablock">
						<div>Server:</div>
						<div>{{info.botInfo.Server}}</div>
					</div>
					<div class="formdatablock">
						<div>Status:</div>
						<div>{{BotStatus[info.botInfo.Status]}}</div>
					</div>
				</div>

				<div class="box">
					<b-field style="margin-bottom: 1em;" groupd>
						<b-input v-model="loadSongUrl" type="text" placeholder="New song link" expanded />
						<b-button type="is-primary" icon-right="plus" @click="addNewSong" />
						<b-button type="is-primary" icon-right="play" @click="playNewSong" />
					</b-field>

					<div v-if="info.song != null" class="media" style="margin-bottom: 1em;">
						<figure class="media-left">
							<p class="image is-64x64">
								<img :src="getCoverUrl()" />
							</p>
						</figure>
						<div class="media-content">
							<div class="field">
								<a :href="info.song.Link" target="_blank">
									<b-icon :icon="typeIcon(info.song.AudioType)" :style="colorIcon(info.song.AudioType)"></b-icon>
									<strong>{{info.song.Title}}</strong>
								</a>
							</div>
						</div>
					</div>

					<div class="title is-5" style="margin-bottom: 0.5em;">Up Next:</div>

					<b-table :data="info.nowPlaying.Items" style="margin-bottom: 1em;" hoverable>
						<template slot-scope="props">
							<b-table-column
								label=" "
								class="is-flex uni-hover"
								:class="{
									'is-selected': (info.nowPlaying.DisplayOffset + props.index) == info.nowPlaying.PlaybackIndex,
									'is-light': (info.nowPlaying.DisplayOffset + props.index) < info.nowPlaying.PlaybackIndex
								}"
							>
								<b-icon :icon="typeIcon(props.row.AudioType)" :style="colorIcon(props.row.AudioType)"></b-icon>
								<span>{{props.row.Title}}</span>
							</b-table-column>
						</template>
						<template slot="empty">Nothing...</template>
					</b-table>
				</div>
			</div>
		</div>

		<play-controls v-if="online" :botId="botId" :info="info" @requestRefresh="refresh" />
	</div>
</template>

<script lang="ts">
import Vue from "vue";
import PlayControls from "../Components/PlayControls.vue";
import BotNavbarItem from "../Components/BotNavbarItem.vue";
import {
	CmdBotInfo,
	CmdPlaylist,
	CmdSong,
	CmdQueueInfo,
	Empty
} from "../ApiObjects";
import { Get, bot, cmd, jmerge } from "../Api";
import { Util } from "../Util";
import { RepeatKind } from "../Model/RepeatKind";
import { BotStatus } from "../Model/BotStatus";
import { BotInfoSync } from "../Model/BotInfoSync";

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

			loadSongUrl: "",

			info: new BotInfoSync()
		};
	},
	computed: {
		botId(): number {
			return Number(this.$route.params.id);
		},
		botName(): string {
			return this.$route.params.name;
		}
	},
	methods: {
		track(val: any) {
			console.log(val);
			return val;
		},
		async refresh() {
			if (this.online) {
				const res = await bot(
					jmerge(
						cmd<CmdBotInfo>("bot", "info"),
						cmd<CmdQueueInfo>("info", "@-1", "5"),
						cmd<CmdSong | null>("song"),
						cmd<RepeatKind>("repeat"),
						cmd<boolean>("random"),
						cmd<number>("volume")
					),
					this.botId
				).get();

				if (!Util.check(this, res, "Failed to get bot information"))
					return;

				this.info.botInfo = res[0] ?? Empty.CmdBotInfo();
				this.info.nowPlaying = res[1] ?? Empty.CmdQueueInfo();
				this.info.song = res[2];
				this.info.repeat = res[3];
				this.info.shuffle = res[4];
				this.info.volume = Math.floor(res[5]);
			} else {
				this.info.botInfo.Id = ("N/A" as any) as number;
				this.info.botInfo.Name = this.botName;
				this.info.botInfo.Status = BotStatus.Offline;
			}
		},
		playNewSong() {
			return this.newSong("play");
		},
		addNewSong() {
			return this.newSong("add");
		},
		async newSong(action: string) {
			const song = this.loadSongUrl;
			this.loadSongUrl = "";
			const res = await bot(
				cmd<CmdBotInfo>(action, song),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to start song")) return;

			await this.refresh();
		},
		typeIcon: Util.typeIcon,
		colorIcon: Util.colorIcon,
		getCoverUrl(): string {
			return (
				Get.Endpoint +
				bot(cmd("data", "song", "cover", "get"), this.botId).done()
			);
		}
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
