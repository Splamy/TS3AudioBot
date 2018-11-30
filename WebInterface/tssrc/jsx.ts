declare namespace JSX {
    type HTMLJsxBase<T extends HTMLElement> = Partial<{ [K in keyof T]: T[K] } & { class: string, when: boolean, set: IJsxGet, style: any }>

    interface IntrinsicElements {
        div: HTMLJsxBase<HTMLDivElement>,
        a: HTMLJsxBase<HTMLAnchorElement & HTMLHyperlinkElementUtils>,
        input: HTMLJsxBase<HTMLInputElement>,
        script: HTMLJsxBase<HTMLScriptElement>,
        link: HTMLJsxBase<HTMLLinkElement>,
    }

    interface Element extends HTMLElement { }
}

interface AttributeMap {
    [key: string]: Content | Function;
}

interface IJsxGet {
    element?: HTMLElement | undefined;
}

type Content = string | boolean | number;

function createElement(tag: string, attrs: AttributeMap, ...children: (Element | Content)[]): HTMLElement | undefined {
    if (attrs && attrs["when"] === false)
        return undefined;

    const el = document.createElement(tag);

    if (attrs && attrs["set"])
        (attrs["set"] as any).element = el;

    for (let name in attrs) {
        if (name && attrs.hasOwnProperty(name)) {
            let value = attrs[name];
            if (name === 'className' && value !== void 0) {
                el.setAttribute('class', value.toString());
            } else if (value === false || value === null || value === undefined || value === true) {
                (el as any)[name] = value;
            } else if (typeof value === 'function') {
                (el as any)[name.toLowerCase()] = value;
            } else if (typeof value === 'object') {
                el.setAttribute(name, value);
            } else {
                el.setAttribute(name, value.toString());
            }
        }
    }

    if (children && children.length > 0) {
        appendChildren(el, children);
    }
    return el;
}

function isElement(el: Element | JSX.Element | any) {
    //nodeType cannot be zero https://developer.mozilla.org/en-US/docs/Web/API/Node/nodeType
    return !!(el as Element).nodeType;
}

function addChild(parentElement: HTMLElement, child: Element | Content | JSX.Element | (Element | Content)[]) {
    if (child === null || child === undefined) {
        return;
    } else if (Array.isArray(child)) {
        appendChildren(parentElement, child);
    } else if (isElement(child)) {
        parentElement.appendChild(child as Element);
    } else {
        parentElement.appendChild(document.createTextNode(child.toString()));
    }
}

function appendChildren(parentElement: HTMLElement, children: (Element | Content)[]) {
    children.forEach(child => addChild(parentElement, child));
}
