<template>
	<section>
		<b-field grouped>
			<b-input icon="magnify" v-model="filter.text" placeholder="Filter..." expanded />

			<b-field>
				<b-radio-button v-model="filter.level" :native-value="0" type="is-success">Simple</b-radio-button>
				<b-radio-button v-model="filter.level" :native-value="1" type="is-warning">Advanced</b-radio-button>
				<b-radio-button v-model="filter.level" :native-value="2" type="is-danger">Expert</b-radio-button>
			</b-field>
		</b-field>

		<settings-group label="General">
			<settings-field :filter="filter" path="run" label="Connect when TS3AudioBot starts">
				<b-switch v-model="model.run" size="is-medium"></b-switch>
			</settings-field>
			<settings-field :filter="filter" path="generate_status_avatar" label="Load song cover as avatar">
				<b-switch v-model="model.generate_status_avatar" size="is-medium"></b-switch>
			</settings-field>
			<settings-field
				:filter="filter"
				path="set_status_description"
				label="Show song in bot description"
			>
				<b-switch v-model="model.set_status_description" size="is-medium"></b-switch>
			</settings-field>

			<settings-field :filter="filter" path="language" label="Bot Language">
				<b-select v-model="model.language" placeholder="Select your language">
					<option value="en">English</option>
					<option value="de">Deutsch (German)</option>
					<option value="ru">Русский (Russian)</option>
				</b-select>
			</settings-field>
		</settings-group>

		<settings-group label="Connection">
			<settings-field :filter="filter" path="connect.name" label="Bot name" grouped>
				<b-input v-model="model.connect.name" minlength="3" maxlength="30" expanded required></b-input>
				<b-button class="control">Apply to bot now (TODO)</b-button>
			</settings-field>
			<settings-field :filter="filter" path="connect.address" label="Server address" grouped>
				<b-input v-model="model.connect.address" expanded required></b-input>
				<b-button class="control">Test (TODO)</b-button>
			</settings-field>

			<settings-field
				:filter="filter"
				path="connect.channel"
				label="Default channel"
			>(Cool dropdown i guess)</settings-field>
		</settings-group>

		<settings-group label="Audio">
			<settings-field :filter="filter" label="Default volume" path="audio.volume.default" grouped>
				<div class="control is-expanded">
					<b-slider v-model="model.audio.volume.default" :min="0" :max="100" lazy></b-slider>
				</div>
				<b-button class="control" @click="model.audio.volume.default = 42">Apply current volume (TODO)</b-button>
			</settings-field>

			<settings-field :filter="filter" label="New song volume" path="audio.volume" advanced>
				<b-slider v-model="bind_volume_reset" :min="0" :max="100" lazy></b-slider>
			</settings-field>

			<settings-field :filter="filter" label="Max user volume" path="audio.max_user_volume" advanced>
				<b-slider v-model="model.audio.max_user_volume" :min="0" :max="100" lazy></b-slider>
			</settings-field>

			<settings-field :filter="filter" label="Bitrate" path="audio.bitrate" grouped advanced>
				<b-field>
					<b-radio-button v-model="model.audio.bitrate" :native-value="16" type="is-danger">Very Poor</b-radio-button>
					<b-radio-button v-model="model.audio.bitrate" :native-value="24" type="is-danger">Poor</b-radio-button>
					<b-radio-button v-model="model.audio.bitrate" :native-value="32" type="is-warning">Okay</b-radio-button>
					<b-radio-button v-model="model.audio.bitrate" :native-value="48" type="is-warning">Good</b-radio-button>
					<b-radio-button v-model="model.audio.bitrate" :native-value="64" type="is-success">Very Good</b-radio-button>
					<b-radio-button v-model="model.audio.bitrate" :native-value="96" type="is-success">Deluxe</b-radio-button>
				</b-field>
				<b-field expanded>
					<b-slider v-model="model.audio.bitrate" :min="2" :max="128" :step="2" expanded></b-slider>
				</b-field>
			</settings-field>
		</settings-group>

		<settings-group label="Commands">
			<settings-field :filter="filter" label="Matcher" path="commands.matcher" expert>
				<b-select v-model="model.commands.matcher" placeholder="Select your matcher">
					<option value="ic3">IC3</option>
					<option value="exact">Exact</option>
					<option value="substring">Substring</option>
				</b-select>
			</settings-field>

			<settings-field
				:filter="filter"
				label="How the bot treats long messages"
				path="commands.long_message"
				advanced
			>
				<b-select
					v-model="model.commands.long_message"
					placeholder="Select how the bot treats long messages"
				>
					<option value="0">Drop (Message will not be sent)</option>
					<option value="1">Split (Message will be split up into multiple messages)</option>
				</b-select>
			</settings-field>
			<settings-field
				:filter="filter"
				label="In how many messages a message can be split max"
				path="commands.long_message_split_limit"
				advanced
			>
				<b-numberinput
					v-model="model.commands.long_message_split_limit"
					controls-position="compact"
					:disabled="model.commands.long_message == 0"
				/>
			</settings-field>
			<settings-field
				:filter="filter"
				label="Max command complexity"
				path="commands.command_complexity"
				expert
			>
				<b-numberinput v-model="model.commands.command_complexity" controls-position="compact" />
			</settings-field>
			<settings-field :filter="filter" label="Colored chat messages" path="commands.color" advanced>
				<b-switch v-model="model.commands.color" size="is-medium"></b-switch>
			</settings-field>
		</settings-group>
	</section>
</template>

<script lang="ts">
import Vue from "vue";
import SettingsField from "../Components/SettingsField.vue";
import SettingsGroup from "../Components/SettingsGroup.vue";
import { bot, cmd } from "../Api";
import { Util } from "../Util";
import Lang from "../Model/Languge";
import { debounce } from "lodash-es";

// missing:
// - channel password
// - server password
// - client version
// - identity

// - send_mode

// - events

// - reconnect

// - aliases

export default Vue.extend({
	props: {
		online: { type: Boolean, required: true }
	},
	data() {
		return {
			Lang,

			filter: {
				text: "",
				level: 0 // 0 simple, 1 advanced, 2 expert
			},
			model: {
				audio: {
					volume: {},
					bitrate: 0
				},
				connect: {},
				commands: {}
			} as any
		};
	},
	async created() {
		const res = await this.requestModel();

		if (!Util.check(this, res, "Failed to retrieve settings")) return;

		this.model = res;

		this.bindRecursive("", this.model);
	},
	watch: {},
	computed: {
		botId(): number | string {
			if (this.online) return Number(this.$route.params.id);
			else return this.$route.params.name;
		},
		bind_volume_reset: {
			get(): [number, number] {
				return [
					this.model.audio.volume.min,
					this.model.audio.volume.max
				];
			},
			set(value: [number, number]) {
				this.model.audio.volume.min = value[0];
				this.model.audio.volume.max = value[1];
			}
		}
	},
	methods: {
		requestModel() {
			if (this.online)
				return bot(cmd<any>("settings", "get"), this.botId).get();
			else
				return cmd<any>(
					"settings",
					"bot",
					"get",
					this.botId.toString()
				).get();
		},
		sendValue(confVal: string, val: string | number) {
			if (this.online)
				return bot(
					cmd<void>("settings", "set", confVal, val.toString()),
					this.botId
				).get();
			else
				return cmd<void>(
					"settings",
					"bot",
					"set",
					this.botId.toString(),
					confVal,
					val.toString()
				).get();
		},
		bindRecursive(path: string, obj: any) {
			for (const childKey of Object.keys(obj)) {
				var child: any = obj[childKey];
				var childPath = (path ? path + "." : "") + childKey;
				if (typeof child === "object" && !Array.isArray(child)) {
					this.bindRecursive(childPath, child);
				} /*if (typeof child === "number" || typeof child === "string") */ else {
					//console.log("binding", childPath);
					this.doWatch(childPath, child);
				}
			}
		},
		doWatch(confVal: string, child: any) {
			this.$watch(
				"model." + confVal,
				debounce(async function(val) {
					const res = await this.sendValue(confVal, val);
					if (!Util.check(this, res, "Failed to apply")) return;

					this.$buefy.toast.open({
						duration: 500,
						message: "Saved",
						type: "is-success"
					});
				}, ...this.getBounceDelay(typeof child))
			);
		},
		getBounceDelay(type: string): [number, object] {
			if (type === "string") {
				return [
					1000,
					{
						leading: false,
						trailing: true
					}
				];
			} else {
				return [
					1000,
					{
						leading: true,
						trailing: true,
						maxWait: 100
					}
				];
			}
		}
	},
	components: {
		SettingsField,
		SettingsGroup
	}
});
</script>
