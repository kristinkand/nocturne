import type { LayoutServerLoad } from "./$types";

/**
 * Root layout server load function
 * Provides user data to all routes
 */
export const load: LayoutServerLoad = async ({ locals }) => {
  return {
    user: locals.user,
    isAuthenticated: locals.isAuthenticated,
  };
};
