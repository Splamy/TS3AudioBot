<template>
	<div
		@click="$emit('click', $event)"
		@mouseover="hovered = true"
		@mouseleave="hovered = false"
		class="clicky-cursor"
	>
		<b-icon :type="{'is-primary': hovered}" :icon="icon" />
	</div>
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.component("hovercon", {
	props: {
		icon: { type: String }
	},
	data() {
		return {
			hovered: false
		};
	}
});
</script>

<style lang="less">
.clicky-cursor {
	cursor: pointer;
}
</style>
