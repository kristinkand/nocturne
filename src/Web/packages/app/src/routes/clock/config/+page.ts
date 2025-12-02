// Clock configuration page load function and utilities
import type { PageLoad } from './$types';

// Element type definition for the LIFO-style clock builder
export type ElementType = 'sg' | 'dt' | 'ar' | 'ag' | 'time' | 'nl';

export interface ClockElement {
  id: string;
  type: ElementType;
  size: number;
}

export interface ClockBuilderConfiguration {
  bgColor: boolean;
  alwaysShowTime: boolean;
  staleMinutes: number;
  elements: ClockElement[];
}

export const load: PageLoad = async ({ url }) => {
  // Extract any URL parameters that might affect configuration
  const searchParams = url.searchParams;
  const presetFace = searchParams.get('face');

  return {
    presetFace,
    meta: {
      title: 'Clock Builder - Nightscout',
      description: 'Build your own custom Nightscout clock display'
    }
  };
};
