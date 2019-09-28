<template>
	<canvas ref="canvas" :width="width" :height="height" style="width: 100%;height: 100%;" />
</template>

<script lang="ts">
import Vue from "vue";

type DrawCallback = (ctx: CanvasRenderingContext2D) => any;

export default Vue.component("v-canvas", {
	props: {
		width: { type: Number, required: true },
		height: { type: Number, required: true }
	},
	mounted() {
		let canvas = this.$refs["canvas"] as HTMLCanvasElement;
		this.$emit("draw", {
			ctx: canvas.getContext("2d")!,
			width: this.width,
			height: this.height
		});
	}
});
</script>

<style lang="less" scoped>
canvas {
	image-rendering: -moz-crisp-edges;
	image-rendering: -webkit-crisp-edges;
	image-rendering: pixelated;
	image-rendering: crisp-edges;
}
</style>
