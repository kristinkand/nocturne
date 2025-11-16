import type { DeviceStatus, Entry, Treatment } from "$lib/api/api-client";

export interface ServerSettings {
  name?: string;
  version?: string;
  apiEnabled?: boolean;
  careportalEnabled?: boolean;
  boluscalcEnabled?: boolean;
  head?: string;
  runtimeState?: string;
  settings?: {
    units?: string;
    timeFormat?: number;
    nightMode?: boolean;
    showRawbg?: string;
    customTitle?: string;
    theme?: string;
    alarmUrgentHigh?: boolean;
    alarmHigh?: boolean;
    alarmLow?: boolean;
    alarmUrgentLow?: boolean;
    alarmTimeagoWarn?: boolean;
    alarmTimeagoWarnMins?: number;
    alarmTimeagoUrgent?: boolean;
    alarmTimeagoUrgentMins?: number;
    language?: string;
    enable?: string;
    showPlugins?: string;
    alarmTypes?: string;
    editMode?: boolean;
    thresholds?: {
      bgHigh?: number;
      bgTargetTop?: number;
      bgTargetBottom?: number;
      bgLow?: number;
    };
    extendedSettings?: any;
  };
  extendedSettings?: any;
  authorized?: {
    role?: string[];
  };
}

export interface ClientSettings {
  units: "mg/dl" | "mmol";
  timeFormat: 12 | 24;
  nightMode: boolean;
  showBGON: boolean;
  showIOB: boolean;
  showCOB: boolean;
  showBasal: boolean;
  showPlugins: string[];
  language: string;
  theme: string;
  alarmUrgentHigh: boolean;
  alarmUrgentHighMins: number[];
  alarmHigh: boolean;
  alarmHighMins: number[];
  alarmLow: boolean;
  alarmLowMins: number[];
  alarmUrgentLow: boolean;
  alarmUrgentLowMins: number[];
  alarmTimeagoWarn: boolean;
  alarmTimeagoWarnMins: number;
  alarmTimeagoUrgent: boolean;
  alarmTimeagoUrgentMins: number;
  showForecast: boolean;
  focusHours: number;
  heartbeat: number;
  baseURL: string;
  authDefaultRoles: string;
  thresholds: unknown;
  demoMode: DemoModeSettings;
}

export interface DemoModeSettings {
  enabled: boolean;
  realTimeUpdates: boolean;
  webSocketUrl: string;
  showDemoIndicators: boolean;
}

export interface Client {
  entries: Entry[];
  treatments: Treatment[];
  deviceStatus: DeviceStatus[];
  settings: ClientSettings;
  now: number;
  latestSGV?: Entry;
  isLoading: boolean;
  isConnected: boolean;
  alarmInProgress: boolean;
  currentAnnouncement?: {
    received: number;
    title: string;
    message: string;
  };
  brushExtent: [Date, Date];
  focusRangeMS: number;
  inRetroMode: boolean;
}

export function getDirectionInfo(
  direction: string
): (typeof directions)[keyof typeof directions] {
  if (direction in directions) {
    return directions[direction as keyof typeof directions];
  }
  return directions["NOT COMPUTABLE"];
}
