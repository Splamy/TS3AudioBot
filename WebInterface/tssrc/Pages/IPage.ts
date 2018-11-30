interface IPage {
    divNav?: HTMLElement

    readonly title?: string;

    init(): Promise<void>;
    refresh(): Promise<void>;
    close?(): Promise<void>;
}
