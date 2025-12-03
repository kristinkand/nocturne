<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import * as Card from "$lib/components/ui/card";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import {
    Loader2,
    Mail,
    CheckCircle,
    ArrowLeft,
    AlertTriangle,
  } from "lucide-svelte";
  import { forgotPasswordForm, getLocalAuthConfig } from "../auth.remote";

  // Query for local auth configuration
  const localAuthQuery = getLocalAuthConfig();

  // Track form submission result
  let submitted = $state(false);
  let adminNotificationRequired = $state(false);

  // Handle form result
  $effect(() => {
    const result = forgotPasswordForm.result;
    if (result?.success) {
      submitted = true;
      adminNotificationRequired = result.adminNotificationRequired ?? false;
    }
  });
</script>

<svelte:head>
  <title>Forgot Password - Nocturne</title>
</svelte:head>

<svelte:boundary>
  {#snippet failed(error)}
    <div
      class="flex min-h-screen items-center justify-center bg-background p-4"
    >
      <Card.Root class="w-full max-w-md">
        <Card.Header class="text-center">
          <Card.Title class="text-2xl font-bold text-destructive">
            Error
          </Card.Title>
        </Card.Header>
        <Card.Content>
          <div
            class="rounded-md bg-destructive/10 p-4 text-sm text-destructive"
          >
            {error.message}
          </div>
          <Button class="mt-4 w-full" onclick={() => window.location.reload()}>
            Try Again
          </Button>
        </Card.Content>
      </Card.Root>
    </div>
  {/snippet}

  {#if localAuthQuery.loading}
    <div
      class="flex min-h-screen items-center justify-center bg-background p-4"
    >
      <Loader2 class="h-8 w-8 animate-spin text-primary" />
    </div>
  {:else}
    {@const localAuth = localAuthQuery.current}
    {@const hasLocalAuth = localAuth?.enabled ?? false}

    <div
      class="flex min-h-screen items-center justify-center bg-background p-4"
    >
      <Card.Root class="w-full max-w-md">
        <Card.Header class="space-y-1 text-center">
          <div
            class="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-primary/10"
          >
            {#if submitted}
              <CheckCircle class="h-6 w-6 text-green-600" />
            {:else}
              <Mail class="h-6 w-6 text-primary" />
            {/if}
          </div>
          <Card.Title class="text-2xl font-bold">
            {#if submitted}
              Check Your Email
            {:else}
              Forgot Password
            {/if}
          </Card.Title>
          <Card.Description>
            {#if submitted}
              We've sent you instructions to reset your password.
            {:else}
              Enter your email and we'll send you a reset link.
            {/if}
          </Card.Description>
        </Card.Header>

        <Card.Content class="space-y-4">
          {#if !hasLocalAuth}
            <div
              class="rounded-lg border border-yellow-200 bg-yellow-50 p-4 dark:border-yellow-900/50 dark:bg-yellow-900/20"
            >
              <p class="text-sm text-yellow-800 dark:text-yellow-200">
                Local authentication is not enabled. Please contact your
                administrator.
              </p>
            </div>
            <Button variant="outline" class="w-full" href="/auth/login">
              <ArrowLeft class="mr-2 h-4 w-4" />
              Back to Login
            </Button>
          {:else if submitted}
            <!-- Success state -->
            <div
              class="rounded-md bg-green-50 dark:bg-green-900/20 p-4 text-sm text-green-800 dark:text-green-200"
            >
              <p>
                If an account exists with that email address, you'll receive a
                password reset link shortly.
              </p>
            </div>

            {#if adminNotificationRequired}
              <div
                class="rounded-md border border-yellow-200 bg-yellow-50 dark:border-yellow-900/50 dark:bg-yellow-900/20 p-4 text-sm"
              >
                <div class="flex gap-3">
                  <AlertTriangle
                    class="h-5 w-5 text-yellow-600 dark:text-yellow-400 flex-shrink-0"
                  />
                  <div class="text-yellow-800 dark:text-yellow-200">
                    <p class="font-medium">Email delivery not configured</p>
                    <p class="mt-1">
                      An administrator has been notified and will contact you
                      with reset instructions.
                    </p>
                  </div>
                </div>
              </div>
            {/if}

            <div class="space-y-2">
              <Button variant="outline" class="w-full" href="/auth/login">
                <ArrowLeft class="mr-2 h-4 w-4" />
                Back to Login
              </Button>
              <p class="text-center text-xs text-muted-foreground">
                Didn't receive an email?
                <button
                  type="button"
                  class="font-medium text-primary hover:underline"
                  onclick={() => (submitted = false)}
                >
                  Try again
                </button>
              </p>
            </div>
          {:else}
            <!-- Forgot password form -->
            <form {...forgotPasswordForm} class="space-y-4">
              <div class="space-y-2">
                <Label for="email">Email Address</Label>
                <div class="relative">
                  <Mail
                    class="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
                  />
                  <Input
                    {...forgotPasswordForm.fields.email.as("email")}
                    id="email"
                    placeholder="you@example.com"
                    class="pl-10"
                    required
                    disabled={!!forgotPasswordForm.pending}
                  />
                </div>
                {#each forgotPasswordForm.fields.email.issues() as issue}
                  <p class="text-sm text-destructive">{issue.message}</p>
                {/each}
              </div>

              {#each forgotPasswordForm.fields.allIssues() as issue}
                <div
                  class="rounded-md bg-destructive/10 p-3 text-sm text-destructive"
                >
                  {issue.message}
                </div>
              {/each}

              <Button
                type="submit"
                class="w-full"
                disabled={!!forgotPasswordForm.pending}
              >
                {#if forgotPasswordForm.pending}
                  <Loader2 class="mr-2 h-4 w-4 animate-spin" />
                  Sending...
                {:else}
                  Send Reset Link
                {/if}
              </Button>
            </form>

            <p class="text-center text-sm text-muted-foreground">
              Remember your password?
              <a
                href="/auth/login"
                class="font-medium text-primary hover:underline"
              >
                Sign in
              </a>
            </p>
          {/if}
        </Card.Content>
      </Card.Root>
    </div>
  {/if}
</svelte:boundary>
