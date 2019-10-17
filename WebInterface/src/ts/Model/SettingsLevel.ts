export enum SettLevel {
	Beginner = 0,
	Advanced = 1,
	Expert = 2,
}

export interface ISettFilter {
	text: string;
	level: SettLevel;
}
