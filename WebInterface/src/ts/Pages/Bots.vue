<!-- UNFINISHED -->

<!-- TODO: Copy bot -->
<!-- TODO: Save running bot -->
<!-- TODO: Jump to settings -->
<!-- TODO: Jump to dashboard -->

<template>
	<div>
		<section class="field">
			<b-field label="Actions">
				<div class="buttons">
					<b-button icon-left="plus" class="is-success" @click="modalCreateBot = true">Create</b-button>
					<b-button icon-left="flash" class="is-success" @click="modalQuickConnect = true">Quick connect</b-button>
				</div>
			</b-field>

			<b-field label="Filter">
				<b-field grouped group-multiline>
					<b-input v-model="showFilter" placeholder="Filter by name and server" expanded></b-input>
					<b-field>
						<b-checkbox-button v-model="showState" native-value="Connected" type="is-success">Connected</b-checkbox-button>
						<b-checkbox-button v-model="showState" native-value="Connecting" type="is-warning">Connecting</b-checkbox-button>
						<b-checkbox-button v-model="showState" native-value="Offline" type="is-danger">Offline</b-checkbox-button>
					</b-field>

					<b-tooltip class="control" label="Clear Filter">
						<b-button class="control" type="is-info" @click="clearFilter">
							<b-icon icon="filter-remove-outline" />
						</b-button>
					</b-tooltip>
				</b-field>
			</b-field>
		</section>

		<b-table
			v-if="displayTiles"
			:data="botsFiltered"
			:hoverable="true"
			:paginated="true"
			:per-page="10"
		>
			<template slot-scope="props">
				<b-table-column field="Id" label="ID" width="40" numeric>
					<b-icon v-if="props.row.Id === null" icon="cancel"></b-icon>
					{{ props.row.Id }}
				</b-table-column>
				<b-table-column field="Name" label="Name">{{ props.row.Name }}</b-table-column>
				<b-table-column field="Server" label="Server">
					<edi-text
						:text="props.row.Server"
						@onedit="editBotServer($event, props.row)"
						:editable="props.row.Name && props.row.Status == BotStatus.Offline"
					/>
				</b-table-column>
				<b-table-column field="Status" label="Status">
					<span :class="'tag ' + statusToColor(props.row.Status)">{{BotStatus[props.row.Status]}}</span>
				</b-table-column>
				<b-table-column width="40">
					<div class="field is-grouped">
						<b-tooltip v-if="props.row.Id == null" class="control" label="Start" :delay="helpDelay">
							<b-button type="is-success" @click="startBot(props.row.Name)">
								<b-icon icon="play" />
							</b-button>
						</b-tooltip>
						<b-tooltip v-else class="control" label="Stop" :delay="helpDelay">
							<b-button type="is-danger" @click="stopBot($event, props.row.Id)">
								<b-icon icon="power" />
							</b-button>
						</b-tooltip>
						<b-tooltip class="control" label="Jump to Server view" :delay="helpDelay">
							<b-button
								:disabled="props.row.Id == null"
								tag="router-link"
								:to="props.row.Id != null
									? { name: 'r_server', params: { id: props.row.Id } }
									: { name: 'r_bots' }"
								type="is-info"
							>
								<b-icon icon="file-tree" />
							</b-button>
						</b-tooltip>
						<b-tooltip class="control" label="Jump to Settings" :delay="helpDelay">
							<b-button
								tag="router-link"
								:to="props.row.Id != null
									? { name: 'r_settings', params: { id: props.row.Id } } 
									: { name: 'r_settings_offline', params: { name: props.row.Name } }"
								type="is-info"
							>
								<b-icon icon="cog" />
							</b-button>
						</b-tooltip>
						<b-dropdown class="control" aria-role="list" position="is-bottom-left">
							<button class="button is-primary" slot="trigger">
								<b-icon icon="dots-horizontal" />
							</button>

							<b-dropdown-item v-show="false" :disabled="props.row.Name == null" aria-role="listitem">
								<b-icon icon="content-copy" />
								<span>Copy</span>
							</b-dropdown-item>
							<b-dropdown-item v-show="false" :disabled="props.row.Name == null" aria-role="listitem">
								<b-icon icon="pencil" />
								<span>Rename</span>
							</b-dropdown-item>
							<b-dropdown-item v-if="props.row.Name" @click="askDeleteBot(props.row.Name)">
								<b-icon icon="delete" type="is-danger" />
								<span>Delete</span>
							</b-dropdown-item>
							<b-dropdown-item v-show="false" v-else>
								<b-icon icon="content-save" type="is-success" />
								<span>Save</span>
							</b-dropdown-item>
						</b-dropdown>
					</div>
				</b-table-column>
			</template>
		</b-table>

		<b-modal :active.sync="modalQuickConnect">
			<QuickConnectModal @callback="connectBot" />
		</b-modal>
		<b-modal :active.sync="modalDeleteBot">
			<DeleteBotModal @callback="deleteBot" :botName="interactBotName" />
		</b-modal>
		<b-modal :active.sync="modalCreateBot">
			<CreateBotModal @callback="createBot" />
		</b-modal>
	</div>
</template>

<script lang="ts">
import Vue from "vue";
import { bot, cmd } from "../Api";
import { CmdBotInfo } from "../ApiObjects";
import { BotStatus } from "../Model/BotStatus";
import { Timer } from "../Timer";
import { Util } from "../Util";
import QuickConnectModal from "../Modals/QuickConnectModal.vue";
import DeleteBotModal from "../Modals/DeleteBotModal.vue";
import CreateBotModal from "../Modals/CreateBotModal.vue";
import EditableText from "../Components/EditableText.vue";

export default Vue.extend({
	data() {
		return {
			BotStatus,
			helpDelay: 500,

			ticker: undefined! as Timer,
			bots: [] as CmdBotInfo[],
			hasConnectingBots: false,

			displayTiles: true,
			showState: ["Connected", "Connecting", "Offline"],
			showFilter: "",
			showLayout: "Table",

			interactBotName: "",
			modalQuickConnect: false,
			modalDeleteBot: false,
			modalCreateBot: false
		};
	},
	computed: {
		botsFiltered(): CmdBotInfo[] {
			if (!this.bots.length) {
				return [];
			}

			return this.bots.filter(item => {
				const connected = this.showState.indexOf("Connected") != -1;
				const connecting = this.showState.indexOf("Connecting") != -1;
				const offline = this.showState.indexOf("Offline") != -1;

				return (
					(item.Name == null ||
						item.Name.indexOf(this.showFilter) >= 0 ||
						(item.Server == null ||
							item.Server.indexOf(this.showFilter) >= 0)) &&
					((offline && item.Status == BotStatus.Offline) ||
						(connecting && item.Status == BotStatus.Connecting) ||
						(connected && item.Status == BotStatus.Connected))
				);
			});
		}
	},
	async created() {
		this.ticker = new Timer(async () => {
			if (!this.hasConnectingBots) {
				this.ticker.stop();
				return;
			}
			await this.refresh();
			if (!this.hasConnectingBots) this.ticker.stop();
		}, 1000);

		await this.refresh();
	},
	destroyed() {
		this.ticker.stop();
	},
	methods: {
		async refresh() {
			const res = await cmd<CmdBotInfo[]>("bot", "list").get();

			if (!Util.check(this, res, "Error getting bot list")) return;

			this.hasConnectingBots = false;
			for (const botInfo of res) {
				if (botInfo.Status === BotStatus.Connecting) {
					this.hasConnectingBots = true;
					break;
				}
			}

			this.bots = res;

			if (this.hasConnectingBots) this.ticker.start();
		},
		clearFilter() {
			this.showState = ["Connected", "Connecting", "Offline"];
			this.showFilter = "";
		},
		statusToColor(status: BotStatus) {
			if (status == BotStatus.Connected) return "is-success";
			else if (status == BotStatus.Connecting) return "is-warning";
			else if (status == BotStatus.Offline) return "is-danger";
			else return "";
		},
		async connectBot(address: string) {
			const res = await cmd<CmdBotInfo>(
				"bot",
				"connect",
				"to",
				address
			).get();
			// TODO get info and jump to corrent page
			if (!Util.check(this, res, "Error connecting bot")) {
				return;
			}
			this.modalQuickConnect = false;
			await this.refresh();
		},
		askDeleteBot(name: string) {
			this.interactBotName = name;
			this.modalDeleteBot = true;
		},
		async deleteBot(name: string) {
			const res = await cmd<void>("settings", "delete", name).get();
			if (!Util.check(this, res, "Error deleting bot")) {
				return;
			}
			this.modalDeleteBot = false;
			await this.refresh();
		},
		async startBot(name: string) {
			const res = await cmd<CmdBotInfo>(
				"bot",
				"connect",
				"template",
				name
			).get();
			if (!Util.check(this, res, "Error starting bot")) {
				return;
			}
			await this.refresh();
		},
		async stopBot(self: MouseEvent, id: number) {
			const res = await bot(cmd<void>("bot", "disconnect"), id).get();
			if (!Util.check(this, res, "Error stopping bot")) {
				return;
			}
			await this.refresh();
		},
		async createBot(name: string) {
			const res = await cmd<void>("settings", "create", name).get();
			if (!Util.check(this, res, "Error creating bot")) {
				return;
			}
			this.modalCreateBot = false;
			await this.refresh();
		},
		async editBotServer(server: string, bot: CmdBotInfo) {
			if (!bot.Name) {
				// TODO: popup warning
				return;
			}

			const res = await cmd<void>(
				"settings",
				"bot",
				"set",
				bot.Name,
				"connect.address",
				server
			).get();

			if (!Util.check(this, res, "Error setting server")) {
				return;
			}

			await this.refresh();
		}
	},
	components: {
		CreateBotModal,
		DeleteBotModal,
		QuickConnectModal,
		EditableText
	}
});

// const res = await cmd<void>(
// 	"settings",
// 	"copy",
// 	name,
// 	i.target
// ).get();
// if (DisplayError.check(res, "Error copying bot")) {
// 	await this.refresh();
// }

// const res = await bot(
// 	cmd<void>("bot", "save", i.name),
// 	botId
// ).get();
// if (DisplayError.check(res, "Error saving bot")) {
// 	await this.refresh();
// }
</script>

<style lang="less">
.table-wrapper {
	overflow-x: unset;
}
</style>
