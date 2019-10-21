<template>
	<div>
		<server-tree />
	</div>
</template>

<script lang="ts">
import Vue from "vue";
import ServerTree from "../Components/ServerTree.vue";

export default Vue.extend({
	data() {
		return {};
	},
	async created() {},
	computed: {
		botId(): number {
			return Number(this.$route.params.id);
		}
	},
	components: {
		ServerTree
	},
	methods: {}
});
</script>
