/// <reference path="Timer.ts"/>

class DisplayError {
	private static readonly fadeDelay: number = 3000;
	private static readonly fadeDuration: number = 1000;

	public static check<T = unknown>(result: T | ApiErr, msg?: string): result is T {
		if (result instanceof ErrorObject) {
			DisplayError.push(result, msg);
			return false;
		}
		return true;
	}

	//public static push(msg: string): void;
	public static push(err?: ApiErr, msg?: string): void {
		let additional: string | undefined = undefined;
		let hasAdditional = false;
		if (msg !== undefined) {
			if (err !== undefined) {
				hasAdditional = true;
				additional = err.obj.ErrorMessage;
			}
		} else if (err !== undefined) {
			msg = err.obj.ErrorMessage;
		} else {
			console.log("Got nothing to show");
			return;
		}

		const divErrors = document.getElementById("errors");
		if (divErrors) {
			let addError = <div class="displayError">
				<div class="formdatablock">
					<div>Error:</div>
					<div>{msg}</div>
				</div>
				<div when={hasAdditional} class="formdatablock">
					<div>Info:</div>
					<div>{additional}</div>
				</div>
			</div>;
			const addedElement = divErrors.appendChild(addError);
			setTimeout(() => addedElement.classList.add("fade"), DisplayError.fadeDelay);
			setTimeout(() => divErrors.removeChild(addedElement), DisplayError.fadeDelay + DisplayError.fadeDuration);
		}
	}
}

class ErrorObject<T=any> {
	constructor(public obj: T) { }
}
