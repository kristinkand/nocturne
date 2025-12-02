import type { RequestHandler } from "./$types";
import { redirect } from "@sveltejs/kit";
import { PUBLIC_API_URL } from "$env/static/public";
import { env } from "$env/dynamic/private";

/**
 * POST handler for logout
 * Calls the API to revoke the session and clears cookies
 */
export const POST: RequestHandler = async ({ fetch, cookies }) => {
  try {
    // Use NOCTURNE_API_URL for server-side (internal Docker network) if available,
    // otherwise fall back to PUBLIC_API_URL for development
    const apiBaseUrl = env.NOCTURNE_API_URL || PUBLIC_API_URL;

    if (apiBaseUrl) {
      // Get the refresh token to send with the logout request
      const refreshToken = cookies.get("nocturne.RefreshToken");

      const headers: HeadersInit = {
        "Content-Type": "application/json",
      };

      // Forward refresh token cookie
      if (refreshToken) {
        headers["Cookie"] = `nocturne.RefreshToken=${refreshToken}`;
      }

      const logoutUrl = new URL("/auth/logout", apiBaseUrl);
      const response = await fetch(logoutUrl.toString(), {
        method: "POST",
        headers,
      });

      if (response.ok) {
        const result = await response.json();

        // Clear all auth cookies
        cookies.delete("nocturne.AccessToken", { path: "/" });
        cookies.delete("nocturne.RefreshToken", { path: "/" });
        cookies.delete("nocturne.IsAuthenticated", { path: "/" });

        // If provider has a logout URL, redirect there
        if (result.providerLogoutUrl) {
          throw redirect(303, result.providerLogoutUrl);
        }
      }
    }

    // Clear cookies even if API call fails
    cookies.delete("nocturne.AccessToken", { path: "/" });
    cookies.delete("nocturne.RefreshToken", { path: "/" });
    cookies.delete("nocturne.IsAuthenticated", { path: "/" });
  } catch (error) {
    // If it's a redirect, re-throw it
    if (error instanceof Response) {
      throw error;
    }

    console.error("Logout error:", error);

    // Clear cookies on error too
    cookies.delete("nocturne.AccessToken", { path: "/" });
    cookies.delete("nocturne.RefreshToken", { path: "/" });
    cookies.delete("nocturne.IsAuthenticated", { path: "/" });
  }

  // Redirect to home page
  throw redirect(303, "/");
};

/**
 * GET handler for logout (for direct link navigation)
 * Shows a confirmation page or redirects immediately based on preference
 */
export const GET: RequestHandler = async ({ fetch, cookies }) => {
  // For GET requests, redirect to a logout confirmation page
  // or perform the logout immediately based on app settings
  // For now, we'll redirect to the login page after clearing cookies

  try {
    const apiBaseUrl = env.NOCTURNE_API_URL || PUBLIC_API_URL;

    if (apiBaseUrl) {
      const refreshToken = cookies.get("nocturne.RefreshToken");

      const headers: HeadersInit = {
        "Content-Type": "application/json",
      };

      if (refreshToken) {
        headers["Cookie"] = `nocturne.RefreshToken=${refreshToken}`;
      }

      const logoutUrl = new URL("/auth/logout", apiBaseUrl);
      await fetch(logoutUrl.toString(), {
        method: "POST",
        headers,
      });
    }
  } catch (error) {
    console.error("Logout error:", error);
  }

  // Clear cookies
  cookies.delete("nocturne.AccessToken", { path: "/" });
  cookies.delete("nocturne.RefreshToken", { path: "/" });
  cookies.delete("nocturne.IsAuthenticated", { path: "/" });

  // Redirect to login page
  throw redirect(303, "/auth/login");
};
