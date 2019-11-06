import { ApiAuth } from "./ApiAuth";
import { ApiError } from "./ApiObjects";

export class ErrorObject<T = any> {
	constructor(public obj: T) { }
}

export class Get {
	public static AuthData: ApiAuth = ApiAuth.Anonymous;
	public static readonly SameAddressEndpoint: string = "/api";
	public static Endpoint: string = Get.SameAddressEndpoint;

	public static async site(site: string): Promise<string> {
		const response = await fetch(site);
		return response.text();
	}

	public static async api<T extends ApiRet>(
		site: Api<T>,
		login: ApiAuth = this.AuthData,
		ep: string = this.Endpoint): Promise<T | ApiErr> {
		// TODO endpoint parameter

		const requestData: RequestInit = {
			method: "GET",
			mode: "cors",
			cache: "no-cache",
			credentials: "same-origin"
		};

		if (!login.IsAnonymous) {
			requestData.headers = new Headers({
				"Authorization": login.getBasic(),
			});
		}

		const apiSite = ep + site.done();
		let response: Response;
		try {
			response = await fetch(apiSite, requestData);
		} catch (err) {
			return new ErrorObject(err);
		}

		let json;
		if (response.status === 204) { // || response.headers.get("Content-Length") === "0"
			json = {};
		} else {
			try {
				// let txt = await response.text();
				// json = JSON.parse(txt);
				json = await response.json();
			} catch (err) {
				return new ErrorObject(err);
			}
		}

		if (!response.ok) {
			json._httpStatusCode = response.status;
			return new ErrorObject(json);
		} else {
			return json as T;
		}
	}
}

export class Api<T extends ApiRet = ApiRet> {
	public constructor(private buildAddr: string) { }

	public static call<T>(...params: (string | number | boolean | Api)[]) {
		let buildStr = "";
		for (const param of params) {
			if (typeof param === "string") {
				buildStr += "/" + encodeURIComponent(param).replace(/\(/, "%28").replace(/\)/, "%29");
			} else if (typeof param === "number" || typeof param === "boolean") {
				buildStr += "/" + param.toString();
			} else if (param instanceof Api) {
				buildStr += "/(" + param.done() + ")";
			} else {
				throw new Error(`Go unknown api object: ${typeof param} ${param}`);
			}
		}
		return new Api<T>(buildStr);
	}

	public async get(): Promise<T | ApiErr> {
		return Get.api<T>(this);
	}

	public done() {
		return this.buildAddr;
	}
}

export function cmd<T extends ApiRet>(...params: (string | number | Api)[]) {
	return Api.call<T>(...params);
}

export function bot<T extends ApiRet>(param: Api<T>, id: number | string) {
	if (typeof id === "number") {
		id = id.toString();
	}
	return Api.call<T>("bot", "use", id, param);
}

export function all<T extends Api[]>(...param: T): Api<UnwrapApi<T>> {
	return Api.call("xecute", ...param);
}

export function jmerge<T extends Api[]>(...param: T): Api<UnwrapApi<T>> {
	return Api.call("json", "merge", ...param);
}

type UnwrapApi<T extends Api[]> = { [K in keyof T]: T[K] extends Api<infer U> ? U : T[K] };

export type ApiRet = {} | null | void;
export type ApiErr = ErrorObject<ApiError>;
