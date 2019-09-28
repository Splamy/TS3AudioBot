<template>
	<form @submit="doCallback" action>
		<div class="modal-card" style="width: auto">
			<header class="modal-card-head">
				<p class="modal-card-title">Enter password</p>
			</header>
			<section class="modal-card-body">
				<b-field label="Password">
					<b-input v-model="password" type="password" required v-focus></b-input>
				</b-field>
			</section>
			<footer class="modal-card-foot">
				<button class="button" type="button" @click="$parent.close()">Cancel</button>
				<button class="button is-primary" type="submit">Ok</button>
			</footer>
		</div>
	</form>
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.extend({
	props: {
		callback: { type: Function }
	},
	data() {
		return {
			password: ""
		};
	},
	methods: {
		doCallback() {
			this.callback(this.password);
			(this.$parent as any).close();
		}
	}
});
</script>
