<template>
	<b-field grouped>
		<b-input v-model="model.pw" @input="notify(true)" type="password" password-reveal expanded></b-input>
		<b-switch v-show="filter.level >= SettLevel.Expert" v-model="model.hashed" @input="notify(false)">Hashed</b-switch>
		<b-switch v-show="filter.level >= SettLevel.Expert" v-model="model.autohash" @input="notify(false)">Hash on read</b-switch>
	</b-field>
</template>

<script lang="ts">
import Vue from "vue";
import { IPassword } from "../ApiObjects";
import { ISettFilter, SettLevel } from "../Model/SettingsLevel";

export default Vue.component("settings-password", {
	props: {
		value: {
			type: Object as () => IPassword,
			required: false
		},
		filter: {
			type: Object as () => ISettFilter,
			required: false,
			default: () => ({ text: "", level: SettLevel.Beginner })
		}
	},
	data() {
		return {
			SettLevel,

			model: {
				pw: "",
				hashed: false,
				autohash: false
			} as IPassword
		};
	},
	watch: {
		value(val: IPassword) {
			this.model = val;
		}
	},
	methods: {
		notify(pw_changed: boolean) {
			if (pw_changed) {
				this.model.hashed = false;
				this.model.autohash = false;
			}
			this.$emit("input", this.model);
		}
	}
});
</script>
