import Vue from "vue";
import { ApiErr, ErrorObject } from "./Api";

export class Util {

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

	public static clearChildren(elem: HTMLElement) {
		while (elem.firstChild) {
			elem.removeChild(elem.firstChild);
		}
	}

	public static clearIcon(elem: HTMLElement) {
		elem.style.backgroundImage = "none";
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
}

export interface Dict<T = any> { [key: string]: T | undefined; }
