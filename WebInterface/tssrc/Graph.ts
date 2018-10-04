class Graph {
    public static buildPath(data: number[], color: string, max?: number) {
        max = max || Math.max(...data);
        let path: string = "M 0 1 ";

        for (let i = 0; i < data.length; i++) {
            const item = data[i];
            path += `L ${i / (data.length - 1)} ${1 - (item / max)} `;
        }
        path += "L 1 1 Z"; // move down and close

        return `<path fill="${color}" d="${path}" />`
    }

    public static readonly border: string = `<path stroke="lightgray" stroke-width="0.001" fill="none" d="M0 0 L0 1 L1 1 L0 1 Z" />`;

    public static buildGrid(count: number, each: number, offset: number) {
        let path: string = "";
        for (let i = 1; i < (count / each) + 1; i++) {
            const lpos = ((i * each) - (offset % each)) / count;
            path += `<path stroke="gray" stroke-width="0.001" stroke-opacity="0.75" fill="none" d="M ${lpos} 0 L ${lpos} 1 Z" />`;
        }
        return path;
    }
}
