interface IPage {
    init(): Promise<void>;
    refresh(): Promise<void>;
}
