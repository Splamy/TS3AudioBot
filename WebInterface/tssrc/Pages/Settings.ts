class Settings {
	public static async get(getStr: Api<any>) {
		const res = await getStr.get();

		if (res instanceof ErrorObject)
			return DisplayError.push(res);

		// deserialize settings here
	}
}