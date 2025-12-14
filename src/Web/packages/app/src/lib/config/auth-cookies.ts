/**
 * Authentication Cookie Configuration
 *
 * Cookie names are configurable via environment variables to match
 * the API's Oidc:Cookie configuration. This ensures consistency
 * between frontend and backend cookie handling.
 */

import { env } from "$env/dynamic/private";

/**
 * Get the access token cookie name from environment or use default
 */
export function getAccessTokenCookieName(): string {
  return env.COOKIE_ACCESS_TOKEN_NAME || ".Nocturne.AccessToken";
}

/**
 * Get the refresh token cookie name from environment or use default
 */
export function getRefreshTokenCookieName(): string {
  return env.COOKIE_REFRESH_TOKEN_NAME || ".Nocturne.RefreshToken";
}

/**
 * Cookie names object for convenience
 */
export const AUTH_COOKIE_NAMES = {
  get accessToken() {
    return getAccessTokenCookieName();
  },
  get refreshToken() {
    return getRefreshTokenCookieName();
  },
  isAuthenticated: "IsAuthenticated",
} as const;
