class Util {

    // tslint:disable no-null-keyword
    public static parseQuery(query: string): any {
        const search = /([^&=]+)=?([^&]*)/g;
        const decode = (s: string) => decodeURIComponent(s.replace(/\+/g, " "));
        const urlParams: any = {};
        let match: RegExpExecArray | null = null;
        do {
            match = search.exec(query);
            if (match === null)
                break;
            urlParams[decode(match[1])] = decode(match[2]);
        } while (match !== null);
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
}
