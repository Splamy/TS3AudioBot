class Home implements IPage {
	//private dummyOffset: number = 0;
	private ticker: Timer = new Timer(async () => await this.refresh(), 1000);

	private static readonly cpuGraphOptions: GraphOptions = {
		color: "red",
		max: Graph.simpleUpFloor,
		offset: 0,
	};
	private static readonly memGraphOptions: GraphOptions = {
		color: "blue",
		max: Graph.simpleUpFloor,
		offset: 0,
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

		res.cpu = Home.padArray(res.cpu, 60, 0);
		var content = "";
		content += Graph.buildPath(res.cpu, Home.cpuGraphOptions);
		//content += Graph.buildGrid(res.cpu.length, 5, ++this.dummyOffset);
		Util.getElementByIdSafe("data_cpugraph").innerHTML = content;

		res.memory = Home.padArray(res.memory, 60, 0);
		content = "";
		content += Graph.buildPath(res.memory, Home.memGraphOptions);
		//content += Graph.buildGrid(res.memory.length, 5, this.dummyOffset);
		Util.getElementByIdSafe("data_memgraph").innerHTML = content;

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
