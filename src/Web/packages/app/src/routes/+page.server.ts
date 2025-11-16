import type { PageServerLoad } from "./$types";

export const load: PageServerLoad = async ({ locals }) => {
  // Initialize realtime store with server data on client
  // Note: This runs on server, client will get the data via PageData

  return {
    initialData: {
      now: Date.now(),
      history: 48, // hours
      focusHours: 3,
    },
  };
};
