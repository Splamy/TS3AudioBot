<template>
	<div v-show="is_visible" class="field settings-field">
		<label class="label">{{label}}</label>
		<!-- <div class="field-label is-normal">
			<label class="label">{{label}}</label>
		</div> -->
		<div class="field-body">
			<b-field :grouped="grouped">
				<slot />
			</b-field>
		</div>
	</div>
</template>

<script lang="ts">
import Vue from "vue";
import SettingsGroup from "./SettingsGroup.vue";

export default Vue.component("settings-field", {
	props: {
		filter: {
			type: Object as () => { text: string; level: number },
			required: false
		},
		path: { type: String, required: true },
		label: { type: String, required: true },
		expert: { type: Boolean, required: false, default: false },
		advanced: { type: Boolean, required: false, default: false },
		grouped: { type: Boolean, required: false, default: false }
	},
	created() {
		this.parentIndex = this.parent_arr.length;
		this.parent_arr.push(true);
	},
	computed: {
		is_visible(): boolean {
			if (this.advanced && this.filter.level < 1) return false;
			if (this.expert && this.filter.level < 2) return false;
			const low_filter = this.filter.text.toLowerCase();
			return (
				this.path.toLowerCase().indexOf(low_filter) >= 0 ||
				this.label.toLowerCase().indexOf(low_filter) >= 0
			);
		},
		parent_arr(): boolean[] {
			return (this.$parent.$parent.$data as any).children as boolean[];
		}
	},
	watch: {
		is_visible(val) {
			Vue.set(this.parent_arr, this.parentIndex, val);
		}
	},
	data() {
		return {
			parentIndex: 0
		};
	},
	methods: {}
});
</script>

<style lang="less">
</style>
