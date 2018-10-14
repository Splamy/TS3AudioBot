class Graph {
    private static readonly scale: number = 100;

    public static buildPath(data: number[], options: GraphOptions) {
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

        return `
        <path fill="${options.color}" d="${path}" />
        <g stroke="gray" stroke-width="1" shape-rendering="crispEdges">
            <line vector-effect="non-scaling-stroke" x1="000" y1="000" x2="100" y2="000" />
            <line vector-effect="non-scaling-stroke" x1="100" y1="000" x2="100" y2="100" />
            <line vector-effect="non-scaling-stroke" x1="100" y1="100" x2="000" y2="100" />
            <line vector-effect="non-scaling-stroke" x1="000" y1="100" x2="000" y2="000" />
            ${Graph.buildGrid(data.length, 5, options.offset++)}
        </g>
        `;
    }

    public static buildGrid(count: number, each: number, offset: number) {
        let path: string = "";
        for (let i = 1; i < (count / each) + 1; i++) {
            const lpos = (((i * each) - (offset % each)) / count) * Graph.scale;
            path += `<line vector-effect="non-scaling-stroke" shape-rendering="crispEdges" stroke-opacity="0.5" x1="${lpos}" y1="0" x2="${lpos}" y2="${Graph.scale}" />`;
        }
        return path;
    }

    public static simpleUpFloor(data: number[]) {
        const max = Math.max(...data);
        return Math.pow(10, Math.ceil(Math.log10(max)));
    }
}

interface GraphOptions {
    color: string;
    max: number | ((data: number[]) => number);
    offset: number;
}
