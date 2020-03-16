<template>
	<form>
		<div class="modal-card" style="width: auto">
			<header class="modal-card-head">
				<p class="modal-card-title">Site settings</p>
			</header>
			<section class="modal-card-body">
				<b-field label="Custom Endpoint">
					<b-input v-model="customEndpoint" placeholder="(leave empty for default: Same address)"></b-input>
				</b-field>
			</section>
			<footer class="modal-card-foot">
				<button class="button is-primary" type="button" @click="$parent.close()">Close</button>
			</footer>
		</div>
	</form>
</template>

<script lang="ts">
import Vue from "vue";
import { Get } from "../Api";

export default Vue.extend({
	data() {
		return {
			customEndpoint:
				Get.Endpoint == Get.SameAddressEndpoint ? "" : Get.Endpoint
		};
	},
	watch: {
		customEndpoint(str: string) {
			Get.Endpoint = str.length == 0 ? Get.SameAddressEndpoint : str;
		}
	}
});
</script>
