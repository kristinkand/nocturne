export interface EventTypeField {
  bg: boolean;
  insulin: boolean;
  carbs: boolean;
  protein: boolean;
  fat: boolean;
  prebolus: boolean;
  duration: boolean;
  percent: boolean;
  absolute: boolean;
  profile: boolean;
  split: boolean;
  sensor: boolean;
}

export interface EventType {
  val: string;
  name: string;
  bg: boolean;
  insulin: boolean;
  carbs: boolean;
  protein: boolean;
  fat: boolean;
  prebolus: boolean;
  duration: boolean;
  percent: boolean;
  absolute: boolean;
  profile: boolean;
  split: boolean;
  sensor: boolean;
}

export const EVENT_TYPES: EventType[] = [
  {
    val: "<none>",
    name: "<none>",
    bg: true,
    insulin: true,
    carbs: true,
    protein: false,
    fat: false,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "BG Check",
    name: "BG Check",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Snack Bolus",
    name: "Snack Bolus",
    bg: true,
    insulin: true,
    carbs: true,
    protein: true,
    fat: true,
    prebolus: true,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Meal Bolus",
    name: "Meal Bolus",
    bg: true,
    insulin: true,
    carbs: true,
    protein: true,
    fat: true,
    prebolus: true,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Correction Bolus",
    name: "Correction Bolus",
    bg: true,
    insulin: true,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Carb Correction",
    name: "Carb Correction",
    bg: true,
    insulin: false,
    carbs: true,
    protein: true,
    fat: true,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Combo Bolus",
    name: "Combo Bolus",
    bg: true,
    insulin: true,
    carbs: true,
    protein: true,
    fat: true,
    prebolus: true,
    duration: true,
    percent: false,
    absolute: false,
    profile: false,
    split: true,
    sensor: false,
  },
  {
    val: "Announcement",
    name: "Announcement",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Note",
    name: "Note",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: true,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Question",
    name: "Question",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Site Change",
    name: "Pump Site Change",
    bg: true,
    insulin: true,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Sensor Start",
    name: "CGM Sensor Start",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: true,
  },
  {
    val: "Sensor Change",
    name: "CGM Sensor Insert",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: true,
  },
  {
    val: "Sensor Stop",
    name: "CGM Sensor Stop",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Pump Battery Change",
    name: "Pump Battery Change",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Insulin Change",
    name: "Insulin Cartridge Change",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Temp Basal Start",
    name: "Temp Basal Start",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: true,
    percent: true,
    absolute: true,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Temp Basal End",
    name: "Temp Basal End",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: true,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
  {
    val: "Profile Switch",
    name: "Profile Switch",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: true,
    percent: false,
    absolute: false,
    profile: true,
    split: false,
    sensor: false,
  },
  {
    val: "D.A.D. Alert",
    name: "D.A.D. Alert",
    bg: true,
    insulin: false,
    carbs: false,
    protein: false,
    fat: false,
    prebolus: false,
    duration: false,
    percent: false,
    absolute: false,
    profile: false,
    split: false,
    sensor: false,
  },
];

/**
 * Get event type configuration by value
 */
export function getEventType(val: string): EventType | undefined {
  return EVENT_TYPES.find(eventType => eventType.val === val);
}

/**
 * Get all event type values
 */
export function getEventTypeValues(): string[] {
  return EVENT_TYPES.map(eventType => eventType.val);
}

/**
 * Get all event type names
 */
export function getEventTypeNames(): string[] {
  return EVENT_TYPES.map(eventType => eventType.name);
}

/**
 * Check if an event type supports a specific field
 */
export function eventTypeSupportsField(eventTypeVal: string, field: keyof EventTypeField): boolean {
  const eventType = getEventType(eventTypeVal);
  if (!eventType) return false;

  // Safely access the field using a type-safe approach
  switch (field) {
    case 'bg':
      return eventType.bg;
    case 'insulin':
      return eventType.insulin;
    case 'carbs':
      return eventType.carbs;
    case 'protein':
      return eventType.protein;
    case 'fat':
      return eventType.fat;
    case 'prebolus':
      return eventType.prebolus;
    case 'duration':
      return eventType.duration;
    case 'percent':
      return eventType.percent;
    case 'absolute':
      return eventType.absolute;
    case 'profile':
      return eventType.profile;
    case 'split':
      return eventType.split;
    case 'sensor':
      return eventType.sensor;
    default:
      return false;
  }
}
