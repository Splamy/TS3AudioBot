<template>
	<form @submit="$emit('callback', list); $parent.close()" action>
		<div class="modal-card" style="width: auto">
			<header class="modal-card-head">
				<p class="modal-card-title">Delete Playlist</p>
			</header>
			<section
				class="modal-card-body"
			>Do you really want to delete the playlist '{{list.Title}}' ({{list.Id}})?</section>
			<footer class="modal-card-foot">
				<button class="button" type="button" @click="$parent.close()">Cancel</button>
				<button class="button is-danger" type="submit">Delete</button>
			</footer>
		</div>
	</form>
</template>

<script lang="ts">
import Vue from "vue";
import { CmdPlaylistInfo } from "../ApiObjects";

export default Vue.extend({
	props: {
		list: { type: Object as () => CmdPlaylistInfo, required: true }
	}
});
</script>
