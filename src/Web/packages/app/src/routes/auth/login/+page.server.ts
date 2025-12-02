import type { PageServerLoad } from "./$types";
import { redirect } from "@sveltejs/kit";
import { PUBLIC_API_URL } from "$env/static/public";
import { env } from "$env/dynamic/private";

/**
 * Provider info from API
 */
interface OidcProviderInfo {
  id: string;
  name: string;
  icon?: string;
  buttonColor?: string;
}

/**
 * Server-side load function for the login page
 * Fetches available OIDC providers and checks auth status
 */
export const load: PageServerLoad = async ({ locals, url, fetch }) => {
  // If user is already authenticated, redirect to return URL or home
  if (locals.isAuthenticated && locals.user) {
    const returnUrl = url.searchParams.get("returnUrl") || "/";
    throw redirect(303, returnUrl);
  }

  // Fetch available OIDC providers from the API
  let providers: OidcProviderInfo[] = [];
  let oidcEnabled = false;

  try {
    // Use NOCTURNE_API_URL for server-side (internal Docker network) if available,
    // otherwise fall back to PUBLIC_API_URL for development
    const apiBaseUrl = env.NOCTURNE_API_URL || PUBLIC_API_URL;

    if (apiBaseUrl) {
      const providersUrl = new URL("/auth/providers", apiBaseUrl);
      const response = await fetch(providersUrl.toString());

      if (response.ok) {
        providers = await response.json();
        oidcEnabled = providers.length > 0;
      }
    }
  } catch (error) {
    console.error("Failed to fetch OIDC providers:", error);
  }

  return {
    providers,
    oidcEnabled,
    returnUrl: url.searchParams.get("returnUrl") || "/",
  };
};
