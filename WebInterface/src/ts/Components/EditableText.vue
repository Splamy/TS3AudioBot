<template>
	<div class="edit-box">
		<b-field v-if="editMode">
			<b-input
				@keyup.native.enter="submit"
				@keyup.native.esc="cancel"
				@blur="cancel"
				v-model="editText"
				v-focus
			/>
			<p class="control">
				<b-button @click="submit" @blur.native="cancel">
					<b-icon icon="check" />
				</b-button>
			</p>
		</b-field>
		<b-field v-else>
			<span :class="{'edit-box-active': editable}">{{ text }}</span>
			<p v-if="editable">
				<hovercon @click="edit" class="edit-button" icon="pencil" />
			</p>
		</b-field>
	</div>
</template>

<script lang="ts">
import Vue from "vue";
import Hovercon from "./Hovercon.vue";
import { Util } from "../Util";

export default Vue.component("edi-text", {
	props: {
		text: { type: String, required: false },
		editable: { type: Boolean, required: false, default: true }
	},
	data() {
		return {
			editMode: false,
			editText: ""
		};
	},
	methods: {
		edit() {
			this.editText = this.text;
			this.editMode = true;
		},
		submit() {
			if (this.text !== this.editText) {
				this.$emit("onedit", this.editText);
			}
			this.editMode = false;
		},
		cancel(e: FocusEvent) {
			if (
				e.relatedTarget &&
				Util.findParent(e.relatedTarget as HTMLElement, ".edit-box")
			)
				return;
			this.editMode = false;
		}
	},
	computed: {},
	components: {
		Hovercon
	}
});
</script>

<style lang="less">
.edit-box {
	display: flex;

	.edit-button {
		opacity: 0;
	}
}

.edit-box:hover .edit-button,
.uni-hover:hover .edit-button {
	opacity: 1;
}

.edit-box-active {
	//border-bottom: 1px dotted #000;
}
</style>
