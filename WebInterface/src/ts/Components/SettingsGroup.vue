<template>
	<b-collapse v-show="is_visible" class="card" aria-id="settings_box">
		<div
			slot="trigger"
			slot-scope="props"
			class="card-header"
			role="button"
			aria-controls="settings_box"
		>
			<p class="card-header-title">{{label}}</p>
			<a class="card-header-icon">
				<b-icon :icon="props.open ? 'menu-down' : 'menu-up'"></b-icon>
			</a>
		</div>
		<div class="card-content">
			<div class="content">
				<slot></slot>
			</div>
		</div>
	</b-collapse>
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.component("settings-group", {
	props: {
		label: {
			type: String,
			required: false
		}
	},
	data() {
		return {
			children: [] as boolean[]
		};
	},
	computed: {
		is_visible(): boolean {
			return this.children.length > 0 && this.children.find(x => x) === true;
		}
	}
});
</script>
