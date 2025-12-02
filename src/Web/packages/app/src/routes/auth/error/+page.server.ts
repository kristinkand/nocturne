import type { PageServerLoad } from "./$types";

/**
 * Server-side load function for the auth error page
 * Extracts error details from query parameters
 */
export const load: PageServerLoad = async ({ url }) => {
  return {
    error: url.searchParams.get("error") || "unknown_error",
    description: url.searchParams.get("description") || "An authentication error occurred",
  };
};
