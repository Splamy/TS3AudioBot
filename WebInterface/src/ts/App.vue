<template>
	<div id="app">
		<Navbar />
		<section class="section">
			<div class="container">
				<router-view />
			</div>
		</section>
	</div>
</template>

<script lang="ts">
import Navbar from "./Components/Navbar.vue";

export default {
	data() {
		return {};
	},
	components: {
		Navbar,
	}
};
</script>

<style lang="less">
html {
	//overflow-y: auto !important;
}
</style>
