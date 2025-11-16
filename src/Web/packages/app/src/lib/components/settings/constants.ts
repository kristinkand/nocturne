import type { SelectOption } from './types.js';
import type { ClientSettings } from '$lib/stores/serverSettings.js';

export const unitOptions: SelectOption[] = [
	{ value: 'mg/dl', label: 'mg/dl' },
	{ value: 'mmol', label: 'mmol/L' }
];

export const timeFormatOptions: SelectOption[] = [
	{ value: '12', label: '12-hour (AM/PM)' },
	{ value: '24', label: '24-hour' }
];

export const themeOptions: SelectOption[] = [
	{ value: 'default', label: 'Default' },
	{ value: 'dark', label: 'Dark' },
	{ value: 'colors', label: 'Colorful' }
];

export const languageOptions: SelectOption[] = [
	{ value: 'en', label: 'English' },
	{ value: 'de', label: 'Deutsch' },
	{ value: 'es', label: 'Español' },
	{ value: 'fr', label: 'Français' },
	{ value: 'it', label: 'Italiano' },
	{ value: 'nl', label: 'Nederlands' }
];

export const alarmMinutesOptions: SelectOption[] = [
	{ value: "1", label: '1 minute' },
	{ value: "2", label: '2 minutes' },
	{ value: "3", label: '3 minutes' },
	{ value: "5", label: '5 minutes' },
	{ value: "10", label: '10 minutes' },
	{ value: "15", label: '15 minutes' },
	{ value: "30", label: '30 minutes' },
	{ value: "60", label: '1 hour' }
];

export const focusHoursOptions: SelectOption[] = [
	{ value: "1", label: '1 hour' },
	{ value: "2", label: '2 hours' },
	{ value: "3", label: '3 hours' },
	{ value: "6", label: '6 hours' },
	{ value: "12", label: '12 hours' },
	{ value: "24", label: '24 hours' },
	{ value: "48", label: '48 hours' }
];

export const pluginOptions: SelectOption[] = [
	{ value: 'delta', label: 'Delta' },
	{ value: 'direction', label: 'Direction' },
	{ value: 'upbat', label: 'Uploader Battery' },
	{ value: 'timeago', label: 'Time Ago' },
	{ value: 'devicestatus', label: 'Device Status' },
	{ value: 'errorcodes', label: 'Error Codes' },
	{ value: 'iob', label: 'IOB (Insulin on Board)' },
	{ value: 'cob', label: 'COB (Carbs on Board)' },
	{ value: 'bwp', label: 'Bolus Wizard Preview' },
	{ value: 'cage', label: 'Cannula Age' },
	{ value: 'sage', label: 'Sensor Age' },
	{ value: 'iage', label: 'Insulin Age' },
	{ value: 'bage', label: 'Battery Age' },
	{ value: 'basal', label: 'Basal Profile' },
	{ value: 'bridge', label: 'Share2Nightscout Bridge' },
	{ value: 'mmconnect', label: 'MiniMed Connect Bridge' },
	{ value: 'pump', label: 'Pump' },
	{ value: 'openaps', label: 'OpenAPS' },
	{ value: 'loop', label: 'Loop' },
	{ value: 'override', label: 'Override' }
];

export function getDefaultSettings(): ClientSettings {
	return {
		units: 'mg/dl',
		timeFormat: 12,
		nightMode: false,
		showBGON: true,
		showIOB: true,
		showCOB: true,
		showBasal: true,
		showPlugins: ['delta', 'direction', 'timeago', 'devicestatus'],
		language: 'en',
		theme: 'default',
		alarmUrgentHigh: true,
		alarmUrgentHighMins: [15, 30, 60],
		alarmHigh: true,
		alarmHighMins: [30, 60],
		alarmLow: true,
		alarmLowMins: [15, 30, 45, 60],
		alarmUrgentLow: true,
		alarmUrgentLowMins: [5, 10, 15, 30],
		alarmTimeagoWarn: true,
		alarmTimeagoWarnMins: 15,
		alarmTimeagoUrgent: true,
		alarmTimeagoUrgentMins: 30,
		showForecast: true,
		focusHours: 3,
		heartbeat: 60,
		baseURL: '',
		authDefaultRoles: 'readable',
		thresholds: {
			high: 260,
			targetTop: 180,
			targetBottom: 80,
			low: 55
		}
	};
}
