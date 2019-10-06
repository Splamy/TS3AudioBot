import Vue from "vue";
import { ApiErr, ErrorObject } from "./Api";

export class Util {
	private static readonly UrlReg = /(https?|ftp):\/\/[^\s\/$.?#].[^\s]*/;

	public static parseQuery(query: string): Dict<string> {
		const search = /(?:[?&])([^&=]+)=([^&]*)/g;
		const decode = (s: string) => decodeURIComponent(s.replace(/\+/g, " "));
		const urlParams: Dict<string> = {};
		let match: RegExpExecArray | null = null;
		do {
			match = search.exec(query);
			if (!match)
				break;
			urlParams[decode(match[1])] = decode(match[2]);
		} while (match);
		return urlParams;
	}

	public static getUrlQuery(): Dict<string> {
		return Util.parseQuery(window.location.href);
	}

	public static buildQuery(data: Dict<string>): string {
		let str = "";
		let hasOne = false;
		for (const dat in data) {
			if (data[dat] == undefined)
				continue;
			str += (hasOne ? "&" : "?") + dat + "=" + data[dat];
			hasOne = true;
		}
		return str;
	}

	public static getElementByIdSafe(elementId: string): HTMLElement {
		return Util.nonNull(document.getElementById(elementId), elementId);
	}

	public static nonNull<T>(elem: T | null, error: string = ""): T {
		if (elem === null) // tslint:disable-line no-null-keyword
			throw new Error(`Missing html element ${error}`);
		return elem;
	}

	public static asError(err: any): ErrorObject {
		return new ErrorObject(err);
	}

	public static parseTimeToSeconds(time: string): number {
		const result = /(\d+):(\d+):(\d+)(?:\.(\d+))?/g.exec(time);
		if (result) {
			let num: number = 0;
			num += Number(result[1]) * 3600;
			num += Number(result[2]) * 60;
			num += Number(result[3]);
			if (result[4]) {
				num += Number(result[4]) / Math.pow(10, result[4].length);
			}
			return num;
		}
		return -1;
	}

	public static formatSecondsToTime(seconds: number): string {
		let str: string = "";
		const h = Math.floor(seconds / 3600);
		if (h > 0) {
			str += h.toString() + ":";
			seconds -= h * 3600;
		}

		const m = Math.floor(seconds / 60);
		str += ("00" + m.toString()).slice(-2) + ":";
		seconds -= m * 60;

		const s = Math.floor(seconds);
		str += ("00" + s.toString()).slice(-2);

		return str;
	}

	public static check<T = unknown>(vue: Vue, result: T | ApiErr, msg?: string): result is T {
		if (result instanceof ErrorObject) {
			let additional: string | undefined;
			let hasAdditional = false;
			if (msg !== undefined) {
				hasAdditional = true;
				additional = result.obj.ErrorMessage;
			} else {
				msg = result.obj.ErrorMessage;
			}

			vue.$buefy.toast.open({
				duration: 3000,
				message: msg,
				type: "is-danger"
			});
			return false;
		}
		return true;
	}

	public static findParent(elem: Node, match: string): HTMLElement {
		let curElement: Node | HTMLElement | null = elem;
		while (curElement != undefined) {
			if ("matches" in curElement && curElement.matches(match)) {
				return curElement;
			}
			curElement = curElement.parentElement;
		}
		throw new Error("Could not find");
	}

	public static findDropLink(data: DataTransfer): string | undefined {
		const plain = data.getData("text/plain");
		const plainRes = this.UrlReg.exec(plain);
		if (plainRes) {
			return plainRes[0];
		}

		const html = data.getData("text/html");
		if (html.length > 0) {
			console.log(html);
			const parser = new DOMParser();
			const htmlDoc = parser.parseFromString(html, "text/html");
			const linkElements = htmlDoc.getElementsByTagName("a");
			if (linkElements.length > 0) {
				return linkElements[0].href;
			}

			const htmlRes = this.UrlReg.exec(html);
			if (htmlRes) {
				return htmlRes[0];
			}
		}
		return undefined;
	}

	public static genImage(name: string, ctx: CanvasRenderingContext2D, width: number = 200, height: number = 200) {

		const size = 5;
		const iter = size * size;
		const cmul = 5;
		const nums: number[] = new Array(cmul).fill(0);
		for (let i = 0; i < name.length; i++) {
			nums[i % cmul] = (nums[i % cmul] + name.charCodeAt(i)) % 17;
		}
		let c = 0;
		let x = 0;
		let y = 0;
		let r = 127;
		let g = 127;
		let b = 127;
		const f: boolean[] = new Array(iter).fill(false);
		for (let i = 0; i < iter; i++) {
			r = (r + Math.cos(nums[c % nums.length]) * 5 + nums[c % nums.length] * Math.sin(c++)) % 255;
			g = (g + Math.cos(nums[c % nums.length]) * 5 + nums[c % nums.length] * Math.sin(c++)) % 255;
			b = (b + Math.cos(nums[c % nums.length]) * 5 + nums[c % nums.length] * Math.sin(c++)) % 255;

			x += (nums[c++ % nums.length] % 3);
			x = (x + size) % size;
			y += (nums[c++ % nums.length] % 3);
			y = (y + size) % size;

			scan: for (let ox = 0; ox < size; ox++) {
				for (let oy = 0; oy < size; oy++) {
					if (!f[x * size + y]) break scan;
					y = (y + 1) % size;
				}
				x = (x + 1) % size;
			}

			f[x * size + y] = true;

			ctx.fillStyle = `rgb(${r}, ${g}, ${b})`;
			ctx.fillRect(x * (width / size), y * (height / size), (width / size), (height / size));
		}
	}

	public static typeIcon(type: string): string {
		switch (type) {
			case "media":
				return "file-music";
			case "youtube":
				return "youtube";
			case "soundcloud":
				return "soundcloud";
			case "twitch":
				return "twitch";
			case "bandcamp":
				return "bandcamp";
			default:
				return "file-question";
		}
	}

	public static colorIcon(type: string): string {
		switch (type) {
			case "media":
				return "";
			case "youtube":
				return "color:#FF0000";
			case "soundcloud":
				return "color:#FE5000";
			case "twitch":
				return "color:#6441A4";
			case "bandcamp":
				return "color:#1DA0C3";
			default:
				return "";
		}
	}
}

export interface Dict<T = any> { [key: string]: T | undefined; }
