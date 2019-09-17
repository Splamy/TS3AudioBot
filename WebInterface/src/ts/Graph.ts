export class Graph {
	private static readonly scale: number = 100;

	public static buildPath(data: number[], node: HTMLElement, options: GraphOptions): void {
		const scale = Graph.scale;
		let max;
		if (typeof options.max === "function")
			max = options.max(data);
		else
			max = options.max;

		let path: string = `M 0 ${scale} `;

		for (let i = 0; i < data.length; i++) {
			const item = data[i];
			path += `L ${(i / (data.length - 1)) * scale} ${(1 - (item / max)) * scale} `;
		}
		path += `L ${scale} ${scale} Z`; // move down and close

		node.innerHTML = `
		${Graph.buildNames(options.scale(max))}
		<svg height="100%" width="100%" viewBox="0 0 100 100" preserveAspectRatio="none" style="overflow: visible;">
			<path vector-effect="non-scaling-stroke" stroke="${options.color}" stroke-opacity="1" fill="${options.color}" fill-opacity="0.4" d="${path}" />
			<g stroke="gray" stroke-width="1" shape-rendering="crispEdges">
				<line vector-effect="non-scaling-stroke" x1="000" y1="000" x2="100" y2="000" />
				<line vector-effect="non-scaling-stroke" x1="100" y1="000" x2="100" y2="100" />
				<line vector-effect="non-scaling-stroke" x1="100" y1="100" x2="000" y2="100" />
				<line vector-effect="non-scaling-stroke" x1="000" y1="100" x2="000" y2="000" />
				${Graph.buildGrid(data.length, 5, options.offset++)}
			</g>
		</svg>
		`;
	}

	public static buildGrid(count: number, each: number, offset: number) {
		let path: string = "";
		for (let i = 1; i < (count / each) + 1; i++) {
			const lpos = (((i * each) - (offset % each)) / count) * Graph.scale;
			path += `<line vector-effect="non-scaling-stroke" shape-rendering="crispEdges" stroke-opacity="0.5" x1="${lpos}" y1="0" x2="${lpos}" y2="${Graph.scale}" />`;
		}
		const scaleQ = Graph.scale / 4;
		for (let i = 1; i < 4; i++) {
			path += `<line vector-effect="non-scaling-stroke" shape-rendering="crispEdges" stroke-opacity="0.5" x1="0" y1="${i * scaleQ}" x2="${Graph.scale}" y2="${i * scaleQ}" />`;
		}
		return path;
	}

	public static buildNames(vals: string[]) {
		let path: string = "";
		for (let i = 0; i < 3; i++) {
			path += `<span style="position: absolute;top: ${100 / 4 * (i + 1)}%;">${vals[i]}</span>`;
		}
		return path;
	}

	public static simpleUpFloor = (data: number[]) => {
		const max = Math.max(...data);
		return Math.pow(10, Math.ceil(Math.log10(max)));
	}

	public static plusNPerc = (data: number[]) => {
		const max = Math.max(...data);
		return max * 1.1;
	}

	public static cpuTrim = (max: number) => {
		const count = 4;
		max *= 100;
		const maxQ = max / count;
		const dec = max <= 10 ? 1 : 0;
		const vals = [];
		for (let i = 0; i < count - 1; i++)
			vals[i] = (maxQ * (i + 1)).toFixed(dec) + "%";
		vals.reverse();
		return vals;
	}

	public static memTrim = (max: number) => {
		const count = 4;

		const maxQ = max / count;
		const vals = [];
		for (let i = 0; i < count - 1; i++) {
			let unit = "B";
			let val = maxQ * (i + 1);
			if (val >= 1_000_000_000) { val /= 1_000_000_000; unit = "GB"; }
			if (val >= 1_000_000) { val /= 1_000_000; unit = "MB"; }
			if (val >= 1_000) { val /= 1_000; unit = "KB"; }
			vals[i] = val.toFixed() + unit;
		}
		vals.reverse();
		return vals;
	}
}

export interface GraphOptions {
	color: string;
	max: number | ((data: number[]) => number);
	offset: number;
	scale: (max: number) => string[];
}

