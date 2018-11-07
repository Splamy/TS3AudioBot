class ModalBox {
	static readonly ModBoxId: string = "modalBox";

	public static show<T extends IModalInput = IModalInput>(msg: string, head: string, options: IModalOptions<T>, ...buttons: IModalButton<T>[]): Promise<void> {
		ModalBox.close();

		// TODO: set focus on first input
		// TODO: listen for enter/esc

		return new Promise(resolve => {
			const inputElem = [];
			const outputs: IModalOutput = {};
			const inputs = options.inputs;
			if (inputs) {
				for (const input in inputs) {
					const inputBox: IJsxGet = {};
					inputElem.push(<div class="formdatablock">
						<div>{inputs[input]}</div>
						<input set={inputBox} name={input} type="text" class="formdatablock_fill" placeholder="" />
					</div>);
					if (inputBox.element) {
						outputs[input] = inputBox.element as HTMLInputElement;
					}
				}
			}

			const btnElem = buttons.map(x => <div class="button"
				onclick={() => {
					if (x.action)
						x.action(ModalBox.transformOutput(outputs) as T);
					ModalBox.close(resolve);
				}}>{x.text}</div>);

			const box = <div class="formbox">
				<div class="formheader flex2">
					<div class="flexmax">{head}</div>
					<div>
						<div class="button buttonRound buttonTiny buttonNoSpace"
							onclick={() => ModalBox.close(resolve)}
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
					<div class="modal_background" onclick={() => { if (options.onCancel) options.onCancel(); ModalBox.close(resolve); }}></div>
					<div class="modal_content">{box}</div>
				</div>
			);
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
	default?: ModalAction;
}

interface IModalOptions<T extends IModalInput> {
	onCancel?: () => void;
	inputs?: T
}

interface IModalInput { [name: string]: string }
interface IModalOutput { [name: string]: HTMLInputElement }

enum ModalAction
{
	/// The default action when pressing 'Enter'
	Ok,
	/// The default action when pressing 'Escape'
	Cancel,
}