import type { Handle } from "@sveltejs/kit";
import { ApiClient } from "$lib/api/api-client";
import type { HandleServerError } from "@sveltejs/kit";
import { PUBLIC_API_URL } from "$env/static/public";
import { env } from "$env/dynamic/private";
import { createHash } from "crypto";

const apiClientHandle: Handle = async ({ event, resolve }) => {
  // Use NOCTURNE_API_URL for server-side (internal Docker network) if available,
  // otherwise fall back to PUBLIC_API_URL for development
  const apiBaseUrl = env.NOCTURNE_API_URL || PUBLIC_API_URL;
  if (!apiBaseUrl) {
    throw new Error(
      "Neither NOCTURNE_API_URL nor PUBLIC_API_URL is defined. Please set one in your environment variables."
    );
  }

  // Get the API secret and hash it with SHA1
  const apiSecret = env.API_SECRET;
  const hashedSecret = apiSecret
    ? createHash("sha1").update(apiSecret).digest("hex").toLowerCase()
    : null;

  // Wrap SvelteKit's fetch to add authentication headers
  const httpClient = {
    fetch: async (url: RequestInfo, init?: RequestInit): Promise<Response> => {
      const headers = new Headers(init?.headers);

      // Add the hashed API secret as authentication
      if (hashedSecret) {
        headers.set("api-secret", hashedSecret);
      }

      return event.fetch(url, {
        ...init,
        headers,
      });
    },
  };

  event.locals.apiClient = new ApiClient(apiBaseUrl, httpClient);

  return resolve(event);
};

export const handleError: HandleServerError = async ({ error, event }) => {
  const errorId = crypto.randomUUID();
  console.error(`Error ID: ${errorId}`, error);
  console.log(
    `Error occurred during request: ${event.request.method} ${event.request.url}`
  );
  return {
    message: "Whoops!",
    errorId,
  };
};

export const handle: Handle = apiClientHandle;
