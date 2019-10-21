<template>
	<li :class="{'is-active': active, 'is-disabled': disabled}">
		<router-link :to="route">
			<span>
				<b-icon :icon="icon" />
			</span>
			<span>{{label}}</span>
		</router-link>
	</li>
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.component("bot-nav-item", {
	props: {
		disabled: { type: Boolean, required: false, default: false },
		label: { type: String, required: true },
		icon: { type: String, required: true },
		page: { type: String, required: true },
		props: { type: Object, required: false }
	},
	methods: {
		goTo() {}
	},
	computed: {
		active(): boolean {
			return (
				this.$route.name == this.page ||
				this.$route.name == this.page + "_offline"
			);
		},
		is_offline(): boolean {
			return !!(
				this.$route.name && this.$route.name.endsWith("_offline")
			);
		},
		route(): Object {
			if (this.page == undefined) return { name: "" };
			return {
				name: this.page + (this.is_offline ? "_offline" : ""),
				params: this.props
			};
		}
	}
});
</script>
