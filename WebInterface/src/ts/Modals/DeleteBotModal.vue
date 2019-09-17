<template>
	<form @submit="$emit('callback', botName)" action>
		<div class="modal-card" style="width: auto">
			<header class="modal-card-head">
				<p class="modal-card-title">Delete Bot</p>
			</header>
			<section class="modal-card-body">Do you really want to delete the bot '{{botName}}'?</section>
			<footer class="modal-card-foot">
				<button class="button" type="button" @click="$parent.close()">Cancel</button>
				<button class="button is-danger" type="submit">Delete</button>
			</footer>
		</div>
	</form>
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.extend({
	props: {
		botName: { type: String }
	}
});
</script>
