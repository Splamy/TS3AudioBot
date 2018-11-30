/// <reference path="Get.ts"/>

class Api<T extends ApiRet = ApiRet> {
	public constructor(private buildAddr: string) { }

	public static call<T>(...params: (string | Api)[]) {
		let buildStr = "";
		for (const param of params) {
			if (typeof param === "string") {
				buildStr += "/" + encodeURIComponent(param);
			} else {
				buildStr += "/(" + param.done() + ")";
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

function cmd<T extends ApiRet>(...params: (string | Api)[]) {
	return Api.call<T>(...params);
}

function bot<T extends ApiRet>(param: Api<T>, id: number | string | undefined = Main.state["bot_id"]) {
	if(id === undefined) {
		throw new Error("The bot id was not set");
	} else if (typeof id === "number") {
		id = id.toString();
	}
	return Api.call<T>("bot", "use", id, param);
}

function jmerge<T extends Api[]>(...param: T): Api<UnwrapApi<T>> {
	return Api.call("json", "merge", ...param);
}

type UnwrapApi<T extends Api[]> = { [K in keyof T]: T[K] extends Api<infer U> ? U : T[K] };
