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

export default Vue.component('bot-nav-item', {
	props: {
		active: { type: Boolean, required: false, default: false },
		disabled: { type: Boolean, required: false, default: false },
		label: { type: String, required: true },
		icon: { type: String, required: true },
		route: { type: String, required: true }
	}
});
</script>
