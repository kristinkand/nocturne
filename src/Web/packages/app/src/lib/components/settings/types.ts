export interface SelectOption {
	value: string;
	label: string;
}

export interface AlarmType {
	enabled: boolean;
	minutes: number[];
}

export type AlarmMinuteType = 'alarmUrgentHighMins' | 'alarmHighMins' | 'alarmLowMins' | 'alarmUrgentLowMins';
