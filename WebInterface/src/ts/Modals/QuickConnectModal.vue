<template>
	<form @submit="$emit('callback', address)" action>
		<div class="modal-card" style="width: auto">
			<header class="modal-card-head">
				<p class="modal-card-title">Connect</p>
			</header>
			<section class="modal-card-body">
				<b-field label="Ip/Domain/Nickname">
					<b-input v-model="address" placeholder="e.g. ts3.teamspeak.com" required v-focus></b-input>
				</b-field>
			</section>
			<footer class="modal-card-foot">
				<button class="button" type="button" @click="$parent.close()">Close</button>
				<button class="button is-primary" type="submit">Connect</button>
			</footer>
		</div>
	</form>
</template>

<script lang="ts">
import Vue from "vue";
import { Util } from "../Util";
import { cmd } from "../Api";

export default Vue.extend({
	data() {
		return {
			address: ""
		};
	}
});
</script>
