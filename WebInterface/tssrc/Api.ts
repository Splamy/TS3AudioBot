class Api {
    private constructor(private buildAddr: string) { }

    public static call(...params: (string | Api)[]): Api {
        let buildStr = "";
        for (const param of params) {
            if (typeof param === "string") {
                buildStr += "/" + encodeURIComponent(param);
            } else {
                buildStr += "/(" + param.done() + ")";
            }
        }
        return new Api(buildStr);
    }

    public done(): string {
        return this.buildAddr;
    }
}
