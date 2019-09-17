<template>
	<b-collapse v-show="is_visible" class="card" aria-id="contentIdForA11y3">
		<div
			slot="trigger"
			slot-scope="props"
			class="card-header"
			role="button"
			aria-controls="contentIdForA11y3"
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
import { get } from "lodash-es";

export default Vue.component("settings-group", {
	props: {
		filter: {
			type: String,
			required: false
		},
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
	created() {},
	computed: {
		is_visible(): boolean {
			return this.children.find(x => x) === true;
		}
	}
});
</script>
