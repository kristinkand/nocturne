import type { PageServerLoad } from "./$types";
import { redirect } from "@sveltejs/kit";

/**
 * Server-side load function for the login page
 * Redirects authenticated users to return URL
 */
export const load: PageServerLoad = async ({ locals, url }) => {
  // If user is already authenticated, redirect to return URL or home
  if (locals.isAuthenticated && locals.user) {
    const returnUrl = url.searchParams.get("returnUrl") || "/";
    throw redirect(303, returnUrl);
  }

  return {};
};
