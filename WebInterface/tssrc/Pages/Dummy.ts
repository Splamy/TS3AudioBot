class Dummy implements IPage {
    divNav?: HTMLElement | undefined;

    private static EmptyPromise: Promise<void> = Promise.resolve();
    init(): Promise<void> { return Dummy.EmptyPromise; }
    refresh(): Promise<void> { return Dummy.EmptyPromise; }
}
