class Dummy implements IPage {
	private static EmptyPromise: Promise<void> = Promise.resolve();
	public init(): Promise<void> { return Dummy.EmptyPromise; }
	public refresh(): Promise<void> { return Dummy.EmptyPromise; }
}
