export class ApiEndpoint {
	public baseAddress: string;
	public sameAddress: boolean;

	public static SameAddress: ApiEndpoint = new ApiEndpoint("/api", true);

	public static Localhost: ApiEndpoint = new ApiEndpoint("http://localhost:58913/api", true);

	public static Splamy: ApiEndpoint = new ApiEndpoint("https://bot.splamy.de/api", true);

	constructor(baseAddress: string, sameAddress: boolean = false) {
		this.baseAddress = baseAddress;
		this.sameAddress = sameAddress;
	}
}
