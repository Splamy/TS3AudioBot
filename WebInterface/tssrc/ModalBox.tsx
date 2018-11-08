class ModalBox {
	static readonly ModBoxId: string = "modalBox";

	public static show<T extends IModalInput = IModalInput>(msg: string, head: string, options: IModalOptions<T>, ...buttons: IModalButton<T>[]): Promise<void> {
		ModalBox.close();

		// TODO: listen for enter/esc

		return new Promise(resolve => {
			const inputElem = [];
			const outputs: IModalOutput = {};
			const inputs = options.inputs;
			let firstInput: HTMLInputElement | undefined;
			if (inputs) {
				for (const input in inputs) {
					const inputBox: IJsxGet = {};
					inputElem.push(<div class="formdatablock">
						<div>{inputs[input]}</div>
						<input set={inputBox} name={input} type="text" class="formdatablock_fill" placeholder="" />
					</div>);
					if (inputBox.element) {
						let inputElem = inputBox.element as HTMLInputElement;
						firstInput = firstInput || inputElem;
						outputs[input] = inputElem;
					}
				}
			}

			const btnElem = [];
			let buttonOk: () => void | undefined;
			for (const addButton of buttons) {
				const doClick = () => {
					if (addButton.action) {
						addButton.action(ModalBox.transformOutput(outputs) as T);
					}
					ModalBox.close(resolve);
				};
				btnElem.push(<div class="button" onclick={doClick}>{addButton.text}</div>);
				if (addButton.default === true) {
					buttonOk = doClick;
				}
			}

			const doCancel = () => { if (options.onCancel) options.onCancel(); ModalBox.close(resolve); };

			const checkKeyPress = (e: KeyboardEvent) =>  {
				if(e.keyCode === 13 && buttonOk) {
					buttonOk();
				} else if(e.keyCode === 27) {
					doCancel();
				}
			}

			document.onkeydown = checkKeyPress;

			const box = <div class="formbox" onkeypress={checkKeyPress} >
				<div class="formheader flex2">
					<div class="flexmax">{head}</div>
					<div>
						<div class="button buttonRound buttonTiny buttonNoSpace"
							onclick={doCancel}
							style="background-image: url(media/icons/x.svg)"></div>
					</div>
				</div>
				<div class="formcontent">
					<div>{msg}</div>
					<div>{inputElem}</div>
					<div class="flex2">
						<div class="flexmax"></div>
						<div class="flex">{btnElem}</div>
					</div>
				</div>
			</div>;

			document.getElementsByTagName("body")[0].appendChild(
				<div id={ModalBox.ModBoxId} class="modal_main">
					<div class="modal_background" onclick={doCancel}></div>
					<div class="modal_content">{box}</div>
				</div>
			);

			if (firstInput)
				firstInput.focus();
		});
	}

	private static transformOutput(output: IModalOutput): IModalInput {
		const input: IModalInput = {};
		for (const out in output) {
			input[out] = output[out].value;
		}
		return input;
	}

	private static close(resolve?: () => void) {
		document.onkeydown = null;
		const modElem = document.getElementById(ModalBox.ModBoxId);
		if (modElem && modElem.parentNode) {
			modElem.parentNode.removeChild(modElem);
		}
		if (resolve)
			resolve();
	}
}

interface IModalButton<T extends IModalInput> {
	text: string;
	action?: (input: T) => void;
	default?: boolean;
}

interface IModalOptions<T extends IModalInput> {
	onCancel?: () => void;
	inputs?: T
}

interface IModalInput { [name: string]: string }
interface IModalOutput { [name: string]: HTMLInputElement }
