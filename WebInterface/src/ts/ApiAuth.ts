export class ApiAuth {

	public get IsAnonymous(): boolean { return this.UserUid.length === 0 && this.Token.length === 0; }

	public static readonly Anonymous: ApiAuth = new ApiAuth("", "");

	constructor(
		public readonly UserUid: string,
		public readonly Token: string) {
	}

	public static Create(fullTokenString: string): ApiAuth {
		if (fullTokenString.length === 0)
			return ApiAuth.Anonymous;

		const split = fullTokenString.split(/:/);
		if (split.length === 2) {
			return new ApiAuth(split[0], split[1]);
		} else if (split.length === 3) {
			return new ApiAuth(split[0], split[2]);
		} else {
			throw new Error("Invalid token");
		}
	}

	public getBasic(): string {
		return `Basic ${btoa(this.UserUid + ":" + this.Token)}`;
	}

	public getFullAuth() {
		return this.UserUid + ":" + this.Token;
	}
}
