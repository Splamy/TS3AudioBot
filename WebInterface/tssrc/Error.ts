class DisplayError {
	public static push(eobj: ErrorObject): void;
	public static push(msg: string, err?: ErrorObject): void;
	public static push(msg: any, err?: any): void {
		let errorData = undefined;
		if (err)
			errorData = err.obj;
		console.log(msg, errorData);
	}
}