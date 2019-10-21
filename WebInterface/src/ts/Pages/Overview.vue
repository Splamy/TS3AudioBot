<template>
	<div class="pure-g">
		<div class="tile is-ancestor">
			<div class="tile is-vertical">
				<div class="tile is-parent">
					<div class="tile is-child notification">
						<span class="title">About</span>
						<div class="formcontent">
							<div class="formdatablock">
								<div>Version:</div>
								<div>{{aboutData.Version}}</div>
							</div>
							<div class="formdatablock">
								<div>Branch:</div>
								<div>{{aboutData.Branch}}</div>
							</div>
							<div class="formdatablock">
								<div>CommitHash:</div>
								<div>{{aboutData.CommitSha}}</div>
							</div>
							<br />
							<div class="formdatablock">
								<div>Uptime:</div>
								<div>{{aboutUptime}}</div>
							</div>
						</div>
					</div>
				</div>
				<div class="tile is-parent">
					<div class="tile is-child">
						<span class="title"></span>
					</div>
				</div>
			</div>
			<div class="tile is-vertical">
				<div class="tile is-parent">
					<div class="tile is-child notification">
						<span class="title">Cpu</span>
						<div id="data_cpugraph" style="position: relative;height: 10em;width: 100%;"></div>
					</div>
				</div>
				<div class="tile is-parent">
					<div class="tile is-child notification">
						<span class="title">Memory</span>
						<div id="data_memgraph" style="position: relative;height: 10em;width: 100%;"></div>
					</div>
				</div>
			</div>
		</div>
	</div>
</template>

<script lang="ts">
import Vue from "vue";
import { cmd } from "../Api";
import { Graph, GraphOptions } from "../Graph";
import { Timer } from "../Timer";
import { Util } from "../Util";

const graphLen = 60;

export default Vue.extend({
	data() {
		return {
			ticker: undefined! as Timer,
			cpuGraphOptions: {
				color: "red",
				max: Graph.plusNPerc,
				offset: 0,
				scale: Graph.cpuTrim
			} as GraphOptions,
			memGraphOptions: {
				color: "blue",
				max: Graph.plusNPerc,
				offset: 0,
				scale: Graph.memTrim
			} as GraphOptions,

			showAbout: true,
			aboutData: {
				Version: "",
				Branch: "",
				CommitSha: ""
			},
			aboutUptime: ""
		};
	},
	async created() {
		this.ticker = new Timer(async () => await this.refresh(), 1000);
		this.ticker.start();

		const res = await cmd<{
			Version: string;
			Branch: string;
			CommitSha: string;
		}>("version").get();

		if (!Util.check(this, res, "Failed to get system information")) return;

		this.aboutData = res;
	},
	destroyed() {
		this.ticker.stop();
	},
	methods: {
		async refresh() {
			const res = await cmd<{
				memory: number[];
				cpu: number[];
				starttime: string;
			}>("system", "info").get();

			if (!Util.check(this, res, "Failed to get system information")) {
				this.ticker.stop();
				return;
			}

			if (!this.ticker.isRunning) {
				this.ticker.start();
			}

			res.cpu = this.padArray(res.cpu, graphLen, 0);
			Graph.buildPath(
				res.cpu,
				Util.getElementByIdSafe("data_cpugraph"),
				this.cpuGraphOptions
			);

			res.memory = this.padArray(res.memory, graphLen, 0);
			Graph.buildPath(
				res.memory,
				Util.getElementByIdSafe("data_memgraph"),
				this.memGraphOptions
			);

			this.aboutUptime = Util.formatSecondsToTime(
				(Date.now() - (new Date(res.starttime) as any)) / 1000
			);
		},
		padArray<T>(arr: T[], count: number, val: T): T[] {
			if (arr.length < count) {
				return Array<T>(count - arr.length)
					.fill(val)
					.concat(arr);
			}
			return arr;
		}
	}
});
</script>
