<template>
	<div>
		<div>{{playlistData.FileName}}</div>
		<ul v-if="playlistData">
			<PlaylistItem v-for="todo in playlistData.Items" :key="todo.Link" :todo="todo" />
		</ul>
		<p v-else>Nothing left in the list. Add a new todo in the input above.</p>
	</div>
</template>

<script lang="ts">
import PlaylistItem from "./PlaylistItem.vue";
import Vue from "vue";
import { bot, cmd } from "../Api";
import { CmdPlaylist } from "../ApiObjects";
import { Util } from "../Util";

export default Vue.component("playlist-editor", {
	components: {
		PlaylistItem
	},
	props: {
		selectedPlaylist: {
			type: String,
			required: false
		}
	},
	data() {
		return {
			playlistData: {
				FileName: "",
				PlaylistName: "",
				SongCount: 0,
				DisplayOffset: 0,
				DisplayCount: 0,
				Items: [] as any[]
			}
		};
	},
	watch: {
		// whenever question changes, this function will run
		async selectedPlaylist(val) {
			if (!this.selectedPlaylist) return;

			const res = await bot(
				cmd<CmdPlaylist>("list", "show", this.selectedPlaylist),
				0 /* TODO */
			).get();

			if (!Util.check(this, res, "Failed to retrieve playlist")) return;

			this.playlistData = res;
		}
	},
	created() {},
	methods: {}
});
</script>
