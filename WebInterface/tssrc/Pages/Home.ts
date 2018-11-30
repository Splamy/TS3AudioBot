class Home implements IPage {
	//private dummyOffset: number = 0;
	private ticker: Timer = new Timer(async () => await this.refresh(), 1000);
	private static readonly graphLen = 60;

	private static readonly cpuGraphOptions: GraphOptions = {
		color: "red",
		max: Graph.plusNPerc,
		offset: 0,
		scale: Graph.cpuTrim,
	};
	private static readonly memGraphOptions: GraphOptions = {
		color: "blue",
		max: Graph.plusNPerc,
		offset: 0,
		scale: Graph.memTrim,
	};

	async init() {
		const res = await cmd<{ Version: string, Branch: string, CommitSha: string }>("version").get();

		if (!DisplayError.check(res, "Failed to get system information"))
			return;

		Util.getElementByIdSafe("data_version").innerText = res.Version;
		Util.getElementByIdSafe("data_branch").innerText = res.Branch;
		Util.getElementByIdSafe("data_commit").innerText = res.CommitSha;

		this.ticker.start();
	}

	async refresh() {
		const res = await cmd<{ memory: number[], cpu: number[], starttime: string }>("system", "info").get();

		if (!DisplayError.check(res, "Failed to get system information")) {
			this.ticker.stop();
			return;
		}

		if (!this.ticker.isRunning) {
			this.ticker.start();
		}

		res.cpu = Home.padArray(res.cpu, Home.graphLen, 0);
		Graph.buildPath(res.cpu, Util.getElementByIdSafe("data_cpugraph"), Home.cpuGraphOptions);

		res.memory = Home.padArray(res.memory, Home.graphLen, 0);
		Graph.buildPath(res.memory, Util.getElementByIdSafe("data_memgraph"), Home.memGraphOptions);

		const timeDiff = Util.formatSecondsToTime((Date.now() - <any>new Date(res.starttime)) / 1000);
		Util.getElementByIdSafe("data_uptime").innerText = timeDiff;
	}

	private static padArray<T>(arr: T[], count: number, val: T): T[] {
		if (arr.length < count) {
			return Array<T>(count - arr.length).fill(val).concat(arr);
		}
		return arr;
	}

	async close() {
		this.ticker.stop();
	}
}
