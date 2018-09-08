class Util {

    // tslint:disable no-null-keyword
    public static parseQuery(query: string): any {
        const search = /([^&=]+)=?([^&]*)/g;
        const decode = (s: string) => decodeURIComponent(s.replace(/\+/g, " "));
        const urlParams: any = {};
        let match: RegExpExecArray | null = null;
        do {
            match = search.exec(query);
            if (!match)
                break;
            urlParams[decode(match[1])] = decode(match[2]);
        } while (match);
        return urlParams;
    }
    // tslint:enable no-null-keyword

    public static parseUrlQuery(url: string): any {
        return Util.parseQuery(url.substr(url.indexOf("?") + 1));
    }

    public static getUrlQuery(): any {
        return Util.parseUrlQuery(window.location.href);
    }

    private static readonly slmax: number = 7.0;
    private static readonly scale: number = 100.0;

    public static value_to_logarithmic(val: number) {
        if (val < 0) val = 0;
        else if (val > Util.slmax) val = Util.slmax;

        return (1.0 / Math.log10(10 - val) - 1) * (Util.scale / (1.0 / Math.log10(10 - Util.slmax) - 1));
    }

    public static logarithmic_to_value(val: number) {
        if (val < 0) val = 0;
        else if (val > Util.scale) val = Util.scale;

        return 10 - Math.pow(10, 1.0 / (val / (Util.scale / (1.0 / Math.log10(10 - Util.slmax) - 1)) + 1));
    }

    public static getElementByIdSafe(elementId: string): HTMLElement {
        return Util.nonNull(document.getElementById(elementId));
    }

    public static nonNull<T>(elem: T | null): T {
        if (elem === null) // tslint:disable-line no-null-keyword
            throw new Error("Missing html element");
        return elem;
    }

    public static clearChildren(elem: HTMLElement) {
        while (elem.firstChild) {
            elem.removeChild(elem.firstChild);
        }
    }

    public static setIcon(elem: HTMLElement, icon: string) {
        elem.style.backgroundImage = `url(/media/icons/${icon}.svg)`;
    }

    public static asError(err: any): ErrorObject {
        return new ErrorObject(err);
    }

    public static parseTimeToSeconds(time: string): number {
        const result = /(\d+):(\d+):(\d+)(?:\.(\d+))?/g.exec(time);
        if (result) {
            let num: number = 0;
            num += Number(result[1]) * 3600;
            num += Number(result[2]) * 60;
            num += Number(result[3]);
            if (result[4]) {
                num += Number(result[4]) / Math.pow(10, result[4].length);
            }
            return num;
        }
        return -1;
    }

    public static formatSecondsToTime(seconds: number): string {
        let str: string = "";
        const h = Math.floor(seconds / 3600);
        if (h > 0) {
            str += h.toString() + ":";
            seconds -= h * 3600;
        }

        const m = Math.floor(seconds / 60);
        str += ("00" + m).slice(-2) + ":";
        seconds -= m * 60;

        const s = Math.floor(seconds);
        str += ("00" + s).slice(-2);

        return str;
    }
}

class ErrorObject {
    constructor(public obj: any) { }
}
