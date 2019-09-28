<template>
	<form @submit="$emit('callback', botName)" action>
		<div class="modal-card" style="width: auto">
			<header class="modal-card-head">
				<p class="modal-card-title">Create Bot</p>
			</header>
			<section class="modal-card-body">
				<b-field label="Bot name">
					<b-input v-model="botName" placeholder="e.g. my_cool_bot" required v-focus></b-input>
				</b-field>
			</section>
			<footer class="modal-card-foot">
				<button class="button" type="button" @click="$parent.close()">Cancel</button>
				<button class="button is-primary" type="submit">Create</button>
			</footer>
		</div>
	</form>
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.extend({
	data() {
		return {
			botName: ""
		};
	}
});
</script>
