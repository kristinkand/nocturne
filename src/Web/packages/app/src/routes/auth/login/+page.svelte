<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import * as Card from "$lib/components/ui/card";
  import { Loader2, KeyRound, ExternalLink } from "lucide-svelte";
  import type { PageData } from "./$types";

  const { data } = $props<{ data: PageData }>();

  let isLoggingIn = $state(false);
  let selectedProvider = $state<string | null>(null);

  /**
   * Initiate login with the specified provider
   */
  function loginWithProvider(providerId: string) {
    isLoggingIn = true;
    selectedProvider = providerId;

    // Build the login URL with provider and return URL
    const params = new URLSearchParams();
    params.set("provider", providerId);
    if (data.returnUrl && data.returnUrl !== "/") {
      params.set("returnUrl", data.returnUrl);
    }

    // Redirect to the auth endpoint
    window.location.href = `/api/auth/login?${params.toString()}`;
  }

  /**
   * Get button style based on provider color
   */
  function getButtonStyle(buttonColor?: string): string {
    if (!buttonColor) return "";
    return `background-color: ${buttonColor}; border-color: ${buttonColor};`;
  }
</script>

<svelte:head>
  <title>Login - Nocturne</title>
</svelte:head>

<div class="flex min-h-screen items-center justify-center bg-background p-4">
  <Card.Root class="w-full max-w-md">
    <Card.Header class="space-y-1 text-center">
      <div class="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-primary/10">
        <KeyRound class="h-6 w-6 text-primary" />
      </div>
      <Card.Title class="text-2xl font-bold">Welcome to Nocturne</Card.Title>
      <Card.Description>
        Sign in to access your glucose data and settings
      </Card.Description>
    </Card.Header>

    <Card.Content class="space-y-4">
      {#if data.oidcEnabled && data.providers.length > 0}
        <div class="space-y-3">
          {#each data.providers as provider}
            <Button
              variant="outline"
              class="w-full h-11 relative"
              style={getButtonStyle(provider.buttonColor)}
              disabled={isLoggingIn}
              onclick={() => loginWithProvider(provider.id)}
            >
              {#if isLoggingIn && selectedProvider === provider.id}
                <Loader2 class="mr-2 h-4 w-4 animate-spin" />
                Redirecting...
              {:else}
                <ExternalLink class="mr-2 h-4 w-4" />
                Sign in with {provider.name}
              {/if}
            </Button>
          {/each}
        </div>

        <div class="relative">
          <div class="absolute inset-0 flex items-center">
            <span class="w-full border-t"></span>
          </div>
          <div class="relative flex justify-center text-xs uppercase">
            <span class="bg-background px-2 text-muted-foreground">
              Secure authentication
            </span>
          </div>
        </div>
      {:else}
        <div class="rounded-lg border border-yellow-200 bg-yellow-50 p-4 dark:border-yellow-900/50 dark:bg-yellow-900/20">
          <p class="text-sm text-yellow-800 dark:text-yellow-200">
            No authentication providers are configured. Please contact your administrator
            to set up OIDC authentication.
          </p>
        </div>
      {/if}

      <div class="text-center text-xs text-muted-foreground">
        <p>
          By signing in, you agree to our 
          <a href="/terms" class="underline hover:text-foreground">Terms of Service</a>
          and
          <a href="/privacy" class="underline hover:text-foreground">Privacy Policy</a>
        </p>
      </div>
    </Card.Content>

    <Card.Footer class="flex flex-col space-y-2">
      <div class="text-center text-xs text-muted-foreground">
        <p>
          Having trouble signing in?
          <a href="/auth/help" class="underline hover:text-foreground">Get help</a>
        </p>
      </div>
    </Card.Footer>
  </Card.Root>
</div>
