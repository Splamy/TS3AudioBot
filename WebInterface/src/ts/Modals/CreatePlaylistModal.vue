<template>
	<form @submit="trySubmit" action>
		<div class="modal-card" style="width: auto">
			<header class="modal-card-head">
				<p class="modal-card-title">Create Bot</p>
			</header>
			<section class="modal-card-body">
				<b-field label="Playlist title">
					<b-input v-model="title" placeholder="e.g. Carpenter Brut - Trilogy" required v-focus></b-input>
				</b-field>
				<b-field
					label="(optional) File name"
					:type="is_taken ? 'is-danger' : ''"
					:message="is_taken ? 'File aready exists. Please enter a name.' : ''"
				>
					<b-field>
						<b-input v-model="fileName" :placeholder="autoFileName" expanded></b-input>
						<span class="button is-static">.ts3ablist</span>
					</b-field>
				</b-field>
			</section>
			<footer class="modal-card-foot">
				<button class="button" type="button" @click="$parent.close()">Cancel</button>
				<button class="button is-primary" type="submit">Create</button>
			</footer>
		</div>
	</form>
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.extend({
	props: {
		existingFiles: {
			type: Array as () => string[],
			required: false,
			default: []
		}
	},
	data() {
		return {
			title: "",
			fileName: ""
		};
	},
	computed: {
		autoFileName(): string {
			if (this.fileName.length > 0) {
				return this.fileName;
			}
			return this.title
				.replace(/\s/g, "_")
				.replace(/[^\w-_]/g, "")
				.substring(0, 64);
		},
		is_taken(): boolean {
			return this.existingFiles.includes(this.autoFileName.toLowerCase());
		}
	},
	methods: {
		trySubmit() {
			if (this.autoFileName.length == 0 || this.is_taken) return;
			this.$emit("callback", this.autoFileName, this.title);
		}
	}
});
</script>
