
<template>
	<div>
		<article class="media">
			<figure class="media-left">
				<a
					class="button is-large is-primary"
					style="border-radius: 100%;"
					@click="$emit('startPlaylist', playlistData.Id)"
				>
					<b-icon icon="play" size="is-large" />
				</a>
			</figure>
			<div class="media-content">
				<div class="content">
					<div class="is-size-2">
						<edi-text :text="playlistData.Title" @onedit="setPlaylistTitle" />
					</div>
					<div class="is-size-4 is-flex">
						<span>File: {{playlistData.Id}}</span>
					</div>
				</div>
			</div>
			<div class="media-right">
				<b-button
					class="is-danger"
					@click="$emit('deletePlaylist', playlistData)"
					icon-left="delete"
				>Delete</b-button>
			</div>
		</article>

		<b-table
			:data="playlistData.Items"
			striped
			paginated
			hoverable
			backend-pagination
			draggable
			:total="total"
			:per-page="perPage"
			@page-change="onPageChange"
			pagination-position="both"
			@dragstart="dragstart"
			@drop="drop"
			@dragover="dragover"
			@dragleave="dragleave"
			:loading="loading"
		>
			<template slot-scope="props">
				<b-table-column label=" " class="is-flex uni-hover">
					<b-icon
						:icon="typeIcon(props.row.AudioType)"
						:style="colorIcon(props.row.AudioType)"
					></b-icon>
					<span>
						<edi-text
							:text="props.row.Title"
							@onedit="itemRename(playlistData.DisplayOffset + props.index, $event)"
						/>
					</span>
				</b-table-column>
				<b-table-column label="  " width="0">
					<div class="field is-grouped song-options">
						<div class="control">
							<b-tooltip label="Play from here">
								<hovercon icon="play" @click="itemPlay(playlistData.DisplayOffset + props.index)" />
							</b-tooltip>
						</div>
						<b-dropdown class="control" aria-role="list" position="is-bottom-left">
							<hovercon icon="dots-horizontal" slot="trigger" />

							<b-dropdown-item
								aria-role="listitem"
								@click="itemRemove(playlistData.DisplayOffset + props.index)"
							>
								<b-icon icon="close-outline"></b-icon>
								<span>Remove</span>
							</b-dropdown-item>
							<b-dropdown-item aria-role="listitem">
								<b-icon icon="book-open-page-variant"></b-icon>
								<span>Move to page</span>
							</b-dropdown-item>
							<b-dropdown-item :has-link="props.row.AudioType != 'media'" aria-role="listitem">
								<a :href="props.row.Link" target="_blank">
									<b-icon icon="link"></b-icon>Source
								</a>
							</b-dropdown-item>
						</b-dropdown>
					</div>
				</b-table-column>
			</template>
			<template slot="empty">
				<section class="section">
					<div v-if="this.selectedPlaylist != none" class="content has-text-grey has-text-centered">
						<p>¯\_(ツ)_/¯</p>
						<p>Playlist is empty.</p>
						<p>Drop a link here to add it...</p>
					</div>
					<div v-else class="content has-text-grey has-text-centered">
						<p>Select a playlist or create a new one to get started.</p>
					</div>
				</section>
			</template>
		</b-table>

		<b-field>
			<b-input
				@keyup.native.enter="addItemClick"
				v-model="addSongLink"
				type="text"
				placeholder="Song link"
				expanded
			/>
			<p class="control">
				<b-button type="is-success" icon-left="plus" @click="addItemClick">Add</b-button>
			</p>
		</b-field>
	</div>
</template>

<script lang="ts">
import Vue from "vue";
import { bot, cmd, all } from "../Api";
import { CmdPlaylist } from "../ApiObjects";
import { Util } from "../Util";
import Hovercon from "./Hovercon.vue";

export default Vue.component("playlist-editor", {
	components: {
		Hovercon
	},
	props: {
		botId: {
			type: Number,
			required: true
		},
		selectedPlaylist: {
			type: String,
			required: true
		}
	},
	data() {
		return {
			none: "<none>",

			loading: false,
			page: 1,
			total: 0,
			perPage: 20,
			draggingRowIndex: undefined as number | undefined,
			addSongLink: "",

			playlistData: {
				Id: "",
				Title: "",
				SongCount: 0,
				DisplayOffset: 0,
				Items: []
			} as CmdPlaylist
		};
	},
	watch: {
		selectedPlaylist(val) {
			this.fetchPlaylist();
		}
	},
	created() {
		this.fetchPlaylist();
	},
	methods: {
		async fetchPlaylist() {
			if (!this.selectedPlaylist || this.selectedPlaylist == this.none) {
				this.loading = false;
				return;
			}

			this.loading = true;

			const res = await bot(
				cmd<CmdPlaylist>(
					"list",
					"show",
					this.selectedPlaylist,
					((this.page - 1) * 20).toString(),
					this.perPage.toString()
				),
				this.botId
			).get();

			this.loading = false;

			if (!Util.check(this, res, "Failed to retrieve playlist")) return;

			this.page = res.DisplayOffset / this.perPage + 1;
			this.total = res.SongCount;
			this.playlistData = res;
		},
		async setPlaylistTitle(title: string) {
			const res = await bot(
				cmd<CmdPlaylist>("list", "name", this.selectedPlaylist, title),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to rename playlist")) return;

			this.playlistData.Title = title;
		},
		addItemClick() {
			this.addItem(this.addSongLink);
			this.addSongLink = "";
		},
		async itemPlay(index: number | undefined) {
			const res = await bot(
				cmd<void>("list", "play", this.selectedPlaylist, index ?? ""),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to start playlist")) return;
		},
		async itemRemove(index: number) {
			this.loading = true;

			const res = await bot(
				cmd<void>(
					"list",
					"item",
					"delete",
					this.selectedPlaylist,
					index
				),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to delete item")) {
				this.loading = false;
				return;
			}

			await this.fetchPlaylist();
		},
		async itemMove(indexFrom: number, indexTo: number) {
			if (indexFrom == indexTo) return;

			this.loading = true;

			const res = await bot(
				cmd<void>(
					"list",
					"item",
					"move",
					this.selectedPlaylist,
					indexFrom,
					indexTo
				),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to move item")) {
				this.loading = false;
				return;
			}

			await this.fetchPlaylist();
		},
		async itemRename(index: number, name: string) {
			this.loading = true;

			const res = await bot(
				cmd<void>(
					"list",
					"item",
					"name",
					this.selectedPlaylist,
					index,
					name
				),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to rename item")) {
				this.loading = false;
				return;
			}

			await this.fetchPlaylist();
		},
		async addItem(link: string, indexTo?: number) {
			if (link.length == 0) return;

			this.loading = true;

			const res = await bot(
				indexTo !== undefined
					? cmd<void>(
							"list",
							"insert",
							this.selectedPlaylist,
							indexTo,
							link
					  )
					: cmd<void>("list", "add", this.selectedPlaylist, link),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to add song")) {
				this.loading = false;
				return;
			}

			await this.fetchPlaylist();
		},
		onPageChange(page: number) {
			this.page = page;
			this.fetchPlaylist();
		},
		dragstart(payload: { event: DragEvent; row: any; index: number }) {
			this.draggingRowIndex = payload.index;
			payload.event.dataTransfer!.setData("Text", payload.row.Title);
			payload.event.dataTransfer!.effectAllowed = "copyMove";
		},
		dragover(payload: { event: DragEvent }) {
			payload.event.dataTransfer!.dropEffect = "move";
			let row = Util.findParent(
				payload.event.target as HTMLElement,
				"tr"
			);
			if (row.classList) {
				row.classList.add("is-selected");
			}
			payload.event.preventDefault();
		},
		dragleave(payload: { event: DragEvent }) {
			let row = Util.findParent(
				payload.event.target as HTMLElement,
				"tr"
			);
			if (row.classList) {
				row.classList.remove("is-selected");
			}
			payload.event.preventDefault();
		},
		drop(payload: { event: DragEvent; index: number }) {
			let row = Util.findParent(
				payload.event.target as HTMLElement,
				"tr"
			);
			row.classList.remove("is-selected");

			payload.event.preventDefault();

			if (this.draggingRowIndex !== undefined) {
				this.itemMove(this.draggingRowIndex, payload.index);
				this.draggingRowIndex = undefined;
			} else if (payload.event.dataTransfer) {
				const link = Util.findDropLink(payload.event.dataTransfer);

				if (link) {
					this.addItem(link, payload.index);
				} else {
					this.$buefy.toast.open({
						message: "Could not find any link",
						type: "is-danger"
					});
				}
			}
		},
		typeIcon: Util.typeIcon,
		colorIcon: Util.colorIcon
	},
	computed: {}
});
</script>

<style lang="less">
[draggable="true"] {
	-khtml-user-drag: element;
	-webkit-user-drag: element;
}
</style>
