<!-- UNFINISHED -->

<template>
	<div class="columns">
		<div class="column is-one-fifth">
			<ul>
				<li
					v-for="list in playlists"
					:key="list.FileName"
					@click="selectedPlaylist = list.FileName"
				>{{ list.FileName }}</li>
			</ul>
		</div>
		<div class="column">
			<playlist-editor :selectedPlaylist="selectedPlaylist" />
		</div>
	</div>
</template>

<script lang="ts">
import Vue from "vue";
import Playlist from "../Components/Playlist.vue";
import { bot, cmd } from "../Api";
import { CmdPlaylistInfo } from "../ApiObjects";
import { Util } from "../Util";

let nextTodoId = 1;

export default Vue.extend({
	components: {
		Playlist
	},
	data() {
		return {
			playlists: [] as CmdPlaylistInfo[],
			selectedPlaylist: null as string | null
		};
	},
	async created() {
		await this.refresh();
	},
	methods: {
		async refresh() {
			const res = await bot(
				cmd<CmdPlaylistInfo[]>("list", "list"),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to retrieve playlists")) {
				return;
			}

			this.playlists = res;
		},
		clickEnter(this: HTMLInputElement, ev: KeyboardEvent) {
			if (ev.key === "Enter") {
				ev.preventDefault();
				// TODO click
				return false;
			}
			return true;
		}
	},
	computed: {
		botId(): number {
			return Number(this.$route.params.id);
		}
	}
});
</script>
