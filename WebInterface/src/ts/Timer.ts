export class Timer {
	public interval: number;
	private readonly func: () => void;
	private timerId: number | undefined;
	public get isRunning(): boolean {
		return this.timerId !== undefined;
	}

	constructor(func: () => void, interval: number) {
		this.func = func;
		this.interval = interval;
	}

	public start(): void {
		if (this.timerId !== undefined)
			return;
		this.timerId = window.setInterval(this.func, this.interval);
	}

	public stop(): void {
		if (this.timerId === undefined)
			return;
		window.clearInterval(this.timerId);
		this.timerId = undefined;
	}

	public restart(): void {
		this.stop();
		this.start();
	}
}
