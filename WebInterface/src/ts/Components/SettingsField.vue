<template>
	<div v-show="is_visible" class="field settings-field is-horizontal is-grouped">
		<div class="field-label">
			<label class="label">
				<span v-if="expert">
					<b-tooltip label="Expert setting" type="is-danger">
						<b-icon icon="alert-circle" type="is-danger"></b-icon>
					</b-tooltip>
				</span>
				<span>{{label}}</span>
			</label>
		</div>
		<div class="field-body">
			<b-field>
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
		filter: { type: String, required: false },
		path: { type: String, required: true },
		label: { type: String, required: true },
		expert: { type: Boolean, required: false, default: false }
	},
	created() {
		this.parentIndex = this.parent_arr.length;
		this.parent_arr.push(true);
	},
	computed: {
		is_visible(): boolean {
			const low_filter = this.filter.toLowerCase();
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
