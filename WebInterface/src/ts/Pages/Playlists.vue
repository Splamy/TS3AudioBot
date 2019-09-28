<template>
	<div>
		<div v-if="editMode">
			<playlist-editor
				:botId="botId"
				:selectedPlaylist="selectedPlaylist"
				@deletePlaylist="deletePlaylistClick"
			/>
		</div>
		<div v-else>
			<b-field>
				<b-input icon="magnify" v-model="filter" placeholder="Filter..." />
			</b-field>

			<div class="playlist-container">
				<div class="playlist-card">
					<a class="pc-content" @click="modalCreatePlaylist = true">
						<div class="pc-pad-box">
							<div class="pc-center">
								<b-icon icon="plus" size="is-large" />
							</div>
							<div class="pc-bottom">
								<p class="card-title title is-5">Create</p>
							</div>
						</div>
					</a>
				</div>

				<div v-for="list in playlists_filter" :key="list.FileName" class="playlist-card">
					<div class="pc-content">
						<div class="pc-pad-box">
							<!-- Padded box -->
							<div class="pc-back">
								<router-link :to="{ name: 'r_playlists', params: { 'playlist': list.FileName }}">
									<v-canvas :width="5" :height="5" @draw="genImage(list.FileName, $event)" />
									<!-- <img src="https://bulma.io/images/placeholders/128x128.png" /> -->
								</router-link>
							</div>
							<div class="pc-center">
								<div class="button is-large is-primary" style="border-radius: 100%;">
									<b-icon icon="play" size="is-large" />
								</div>
							</div>
							<div class="pc-bottom">
								<p class="card-title title is-5">{{ list.FileName }}</p>
							</div>
						</div>
						test
					</div>
				</div>
			</div>
		</div>

		<b-modal :active.sync="modalCreatePlaylist">
			<CreatePlaylistModal
				@callback="createPlaylist"
				:existingFiles="playlists.map(x => x.FileName.toLowerCase())"
			/>
		</b-modal>
	</div>
</template>

<script lang="ts">
import Vue from "vue";
import PlaylistEditor from "../Components/PlaylistEditor.vue";
import VCanvas from "../Components/VCanvas.vue";
import { bot, cmd } from "../Api";
import { CmdPlaylistInfo } from "../ApiObjects";
import { Util } from "../Util";
import CreatePlaylistModal from "../Modals/CreatePlaylistModal.vue";
import DeletePlaylistModal from "../Modals/DeletePlaylistModal.vue";

let nextTodoId = 1;

export default Vue.extend({
	components: {
		VCanvas,
		PlaylistEditor,
		CreatePlaylistModal,
		DeletePlaylistModal
	},
	data() {
		return {
			filter: "",
			playlists: [] as CmdPlaylistInfo[],

			modalCreatePlaylist: false,
			modalDeletePlaylist: false
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
		async createPlaylist(fileName: string, title: string) {
			this.modalCreatePlaylist = false;

			const res = await bot(
				cmd<void>("list", "create", fileName, title),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to create playlist")) {
				return;
			}

			this.$router.push({
				name: "r_playlists",
				params: { playlist: fileName }
			});

			// update playlists in the background
			await this.refresh();
		},
		async deletePlaylistClick(list: CmdPlaylistInfo) {
			this.modalDeletePlaylist = true;

			this.$buefy.modal.open({
				parent: this,
				component: DeletePlaylistModal,
				hasModalCard: true,
				props: {
					list: list
				},
				events: {
					callback: this.deletePlaylist
				}
			});
		},
		async deletePlaylist(list: CmdPlaylistInfo) {
			this.modalDeletePlaylist = false;

			const res = await bot(
				cmd<void>("list", "delete", list.FileName),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to delete playlist")) {
				return;
			}

			this.$router.push({
				name: "r_playlists",
				params: { playlist: "<none>" }
			});

			// update playlists in the background
			await this.refresh();
		},
		genImage(name: string, ev: any) {
			Util.genImage(name, ev.ctx, ev.width, ev.height);
		}
	},
	computed: {
		botId(): number {
			return Number(this.$route.params.id);
		},
		selectedPlaylist(): string {
			return this.$route.params.playlist;
		},
		editMode(): boolean {
			return this.selectedPlaylist != "<none>";
		},
		playlists_filter(): CmdPlaylistInfo[] {
			if (this.filter.length == 0) return this.playlists;
			const lc_filter = this.filter.toLowerCase();
			return this.playlists.filter(
				list =>
					list.FileName.toLowerCase().includes(lc_filter) ||
					(list.PlaylistName &&
						list.PlaylistName.toLowerCase().includes(lc_filter))
			);
		}
	}
});
</script>

<style lang="less">
.playlist-container {
	display: flex;
	flex-wrap: wrap;
}

.playlist-card {
	width: 25%;
	position: relative;

	&:after {
		content: "";
		display: block;
		padding-bottom: 100%;
	}
}

.pc-content {
	position: absolute;
	width: 100%;
	height: 100%;

	padding: 0.5em;
}

.pc-pad-box {
	position: relative;
	width: 100%;
	height: 100%;

	&:hover {
		box-shadow: 0px 0px 10px 3px lightgray;
	}
}

.fill() {
	left: 0;
	top: 0;
	right: 0;
	bottom: 0;

	pointer-events: none;

	> * {
		pointer-events: auto;
	}
}

.pc-back {
	.fill();
	position: absolute;
}

.pc-center {
	.fill();
	display: flex;
	position: absolute;
	justify-content: center;
	align-items: center;
}

.pc-bottom {
	.fill();
	display: flex;
	position: absolute;
	justify-content: center;
	align-items: end;
}

.card-title {
	position: absolute;
	text-overflow: ellipsis;
	white-space: nowrap;
	overflow: hidden;
	padding: 0.5em;
	width: 100%;
	text-align: center;
}
</style>
