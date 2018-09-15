interface IPage {
    divNav?: HTMLElement
    
    init(): Promise<void>;
    refresh(): Promise<void>;
}
