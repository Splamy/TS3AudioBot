<template>
	<section id="playcontrols" class="navbar is-fixed-bottom has-shadow">
		<div style="width:100%;">
			<div class="container" style="margin: 0.5rem auto;">
				<b-field class="flatten-footer" style="align-items: center;" grouped>
					<!-- Playback -->
					<b-button class="is-text" @click="clickRepeat">
						<b-icon :icon="repeat_icon" />
					</b-button>
					<b-button class="is-text" @click="clickShuffle">
						<b-icon :icon="shuffle_icon" />
					</b-button>
					<b-button class="is-text" @click="clickTrackPrev">
						<b-icon icon="skip-previous" />
					</b-button>
					<b-button class="is-text" @click="clickPlay">
						<b-icon class="is-medium" :icon="play_icon" />
					</b-button>
					<b-button class="is-text" @click="clickTrackNext">
						<b-icon icon="skip-next" />
					</b-button>
					<!-- Time slider -->
					<span class="tag is-light control">{{song_position_human}}</span>
					<b-field expanded>
						<b-slider
							class="control"
							@change="setPosition"
							v-model="song_pos_safe"
							:tooltip="false"
							:max="song_length_safe"
							:disabled="!info.song"
							rounded
							lazy
						></b-slider>
					</b-field>
					<span class="tag is-light control">{{song_length_human}}</span>
					<!-- Volume -->
					<b-button class="control is-text" @click="clickVolume">
						<b-icon :icon="volume_icon" />
					</b-button>
					<b-field class="control">
						<b-slider v-model="info.volume" :min="0" :max="100" style="width:10em;"></b-slider>
					</b-field>
				</b-field>
			</div>
		</div>
	</section>
</template>

<script lang="ts">
import Vue from "vue";
import { RepeatKind } from "../Model/RepeatKind";
import { Util } from "../Util";
import { bot, jmerge, cmd, ApiErr, all } from "../Api";
import { PlayState } from "../Model/PlayState";
import { CmdSong } from "../ApiObjects";
import { Timer } from "../Timer";
import { debounce } from "lodash-es";
import { BotInfoSync } from "../Model/BotInfoSync";

export default Vue.component("play-controls", {
	props: {
		botId: { type: Number, required: true },
		info: { type: Object as () => BotInfoSync, required: true }
	},
	async created() {
		this.playTick = new Timer(() => {
			if (!this.info.song) {
				this.playTick.stop();
				return;
			}
			if (this.info.song.Position < this.info.song.Length) {
				this.info.song.Position += 1;
			} else {
				this.playTick.stop();
				this.startEcho();
			}
		}, 1000);

		this.echoTick = new Timer(async () => {
			this.echoCounter += 1;
			if (
				this.echoCounter === 1 ||
				this.echoCounter === 3 ||
				this.echoCounter === 6
			) {
				await this.refresh();
			}
			if (this.echoCounter >= 6) {
				this.echoTick.stop();
			}
		}, 1000);

		this.$watch("info.song", this.updateTimers, { deep: true });
	},
	data() {
		return {
			RepeatKind,
			PlayState,

			volume_old: 0,
			muteToggleVolume: 0,

			echoCounter: 0,
			echoTick: undefined! as Timer,
			playTick: undefined! as Timer
		};
	},
	computed: {
		song_pos_safe: {
			get(): number {
				if (!this.info.song) return 0;
				return this.info.song.Position;
			},
			set(val: number) {
				if (this.info.song) this.info.song.Position = val;
			}
		},
		song_length_safe(): number {
			if (!this.info.song) return 0;
			return this.info.song.Length;
		},
		song_position_human(): string {
			if (!this.info.song) return "--:--";
			return Util.formatSecondsToTime(this.info.song.Position);
		},
		song_length_human(): string {
			if (!this.info.song) return "--:--";
			return Util.formatSecondsToTime(this.info.song.Length);
		},
		playing(): PlayState {
			if (!this.info.song) return PlayState.Off;
			else if (this.info.song.Paused) return PlayState.Paused;
			else return PlayState.Playing;
		},
		play_icon() {
			switch (this.playing) {
				case PlayState.Off:
					return "heart";
				case PlayState.Playing:
					return "pause";
				case PlayState.Paused:
					return "play";
				default:
					throw Error();
			}
		},
		repeat_icon() {
			switch (this.info.repeat) {
				case RepeatKind.Off:
					return "repeat-off";
				case RepeatKind.One:
					return "repeat-once";
				case RepeatKind.All:
					return "repeat";
				default:
					throw Error();
			}
		},
		shuffle_icon(): string {
			return this.info.shuffle ? "shuffle" : "shuffle-disabled";
		},
		volume_icon(): string {
			if (this.info.volume <= 0.001) return "volume-off";
			else if (this.info.volume <= 33) return "volume-low";
			else if (this.info.volume <= 66) return "volume-medium";
			else return "volume-high";
		},
		setVD(): Function {
			return debounce(this.setVolume, 500, {
				maxWait: 100
			});
		}
	},
	methods: {
		async clickRepeat() {
			const res = await bot(
				jmerge(
					cmd<void>(
						"repeat",
						RepeatKind[(this.info.repeat + 1) % 3].toLowerCase()
					),
					cmd<RepeatKind>("repeat")
				),
				this.botId
			).get();
			if (!Util.check(this, res, "Failed to apply repeat mode")) return;

			this.info.repeat = res[1];
		},
		async clickShuffle() {
			const res = await bot(
				jmerge(
					cmd<void>("random", !this.info.shuffle ? "on" : "off"),
					cmd<boolean>("random")
				),
				this.botId
			).get();
			if (!Util.check(this, res, "Failed to apply random mode")) return;

			this.info.shuffle = res[1];
		},
		async clickVolume() {
			if (this.muteToggleVolume !== 0 && this.info.volume === 0) {
				await this.setVolume(this.muteToggleVolume);
				this.muteToggleVolume = 0;
			} else {
				this.muteToggleVolume = this.info.volume;
				await this.setVolume(0);
			}
		},
		async setVolume(value: number) {
			const res = await bot(
				jmerge(
					cmd<void>("volume", value.toString()),
					cmd<number>("volume")
				),
				this.botId
			).get();
			if (!Util.check(this, res, "Failed to apply volume")) {
				this.info.volume = this.volume_old;
				return;
			}
			this.volume_old = this.info.volume;
		},
		async clickTrackNext() {
			const res = await bot(cmd<void>("next"), this.botId).get();
			if (!Util.check(this, res, "Failed to skip forward")) return;
			this.startEcho();
		},
		async clickTrackPrev() {
			const res = await bot(cmd<void>("previous"), this.botId).get();
			if (!Util.check(this, res, "Failed to skip forward")) return;
			this.startEcho();
		},
		async clickPlay() {
			let songRet: ApiErr | [void, CmdSong | null];
			switch (this.playing) {
				case PlayState.Off:
					return;

				case PlayState.Playing:
					songRet = await bot(
						jmerge(cmd<void>("pause"), cmd<CmdSong | null>("song")),
						this.botId
					).get();
					this.playTick.stop();
					break;

				case PlayState.Paused:
					songRet = await bot(
						jmerge(cmd<void>("play"), cmd<CmdSong | null>("song")),
						this.botId
					).get();
					this.playTick.start();
					break;

				default:
					throw new Error();
			}

			if (!Util.check(this, songRet)) return;

			this.info.song = songRet[1];
			this.startEcho();
		},
		async setPosition(value: number) {
			if (this.playing === PlayState.Off) return;

			const wasRunning = this.playTick.isRunning;
			this.playTick.stop();
			const targetSeconds = Math.floor(value);
			const res = await bot(
				cmd<void>("seek", targetSeconds.toString()),
				this.botId
			).get();

			if (!Util.check(this, res, "Failed to seek")) return;

			if (wasRunning) this.playTick.start();
			if (this.info.song) this.info.song.Position = targetSeconds;
		},
		startEcho() {
			this.echoCounter = 0;
			this.echoTick.start();
		},
		async refresh() {
			this.$emit("requestRefresh");
		}, 
		updateTimers() {
			if (this.playing == PlayState.Playing) this.playTick.start();
			else this.playTick.stop();
		}
	},
	watch: {
		"info.volume"(value: number) {
			this.setVD(value);
		}
	}
});
</script>

<style lang="less">
.flatten-footer > .field {
	margin-bottom: 0;
}

#playcontrols {
	height: 3.5rem;
}
</style>
