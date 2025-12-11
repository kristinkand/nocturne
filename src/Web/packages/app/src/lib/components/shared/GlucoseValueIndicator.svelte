<script lang="ts">
  import * as Tooltip from "$lib/components/ui/tooltip";
  import { Skeleton } from "$lib/components/ui/skeleton";

  interface Props {
    /** Glucose value to display (already formatted for units) */
    displayValue: string;
    /** Raw glucose value in mg/dL for color calculation */
    rawBgMgdl: number;
    /** Whether the data is still loading (no data received yet) */
    isLoading?: boolean;
    /** Whether the data is stale (old) */
    isStale?: boolean;
    /** Whether the connection is disconnected */
    isDisconnected?: boolean;
    /** Status text to show (e.g., "1 min ago" or "Connection Error") */
    statusText?: string;
    /** Tooltip text for status (e.g., "Last reading: 5 min ago") */
    statusTooltip?: string;
    /** Size variant - 'sm' for sidebar, 'lg' for dashboard */
    size?: "sm" | "lg";
    /** Additional CSS classes for the container */
    class?: string;
  }

  let {
    displayValue,
    rawBgMgdl,
    isLoading = false,
    isStale = false,
    isDisconnected = false,
    statusText,
    statusTooltip,
    size = "lg",
    class: className = "",
  }: Props = $props();

  // Track value changes to trigger pulse animation
  let isPulsing = $state(false);
  let previousValue = $state<string | null>(null);

  // Trigger pulse animation when displayValue changes
  $effect(() => {
    // Skip initial render and only pulse on actual value changes
    if (
      previousValue !== null &&
      displayValue !== previousValue &&
      !isLoading
    ) {
      isPulsing = true;
      // Remove the class after animation completes
      const timeout = setTimeout(() => {
        isPulsing = false;
      }, 600); // Match animation duration
      return () => clearTimeout(timeout);
    }
    previousValue = displayValue;
  });

  // Get background color based on BG value (only when not stale)
  const getBGColor = (bg: number, stale: boolean) => {
    if (stale) return "bg-muted text-muted-foreground";
    if (bg < 70) return "bg-destructive text-destructive-foreground";
    if (bg < 80) return "bg-yellow-500 text-black";
    if (bg > 250) return "bg-destructive text-destructive-foreground";
    if (bg > 180) return "bg-orange-500 text-black";
    return "bg-green-500 text-white";
  };

  // Get border style based on connection status
  const getBorderStyle = (disconnected: boolean, stale: boolean) => {
    const baseClasses = "border-2";
    if (stale && disconnected) {
      return `${baseClasses} border-dashed border-muted-foreground/50 animate-flash-border`;
    }
    if (disconnected) {
      return `${baseClasses} border-dashed border-current`;
    }
    return ""; // No special border when connected
  };

  const sizeClasses = $derived(
    size === "lg" ? "text-4xl px-4 py-2" : "text-3xl px-3 py-1.5"
  );

  const skeletonSizeClasses = $derived(
    size === "lg" ? "h-12 w-20" : "h-10 w-16"
  );
</script>

<div class="glucose-value-indicator inline-flex items-center gap-2 {className}">
  {#if isLoading}
    <!-- Loading skeleton -->
    <Skeleton class="rounded-lg {skeletonSizeClasses}" />
    <div class="flex flex-col gap-1">
      <Skeleton class="h-4 w-12" />
      <Skeleton class="h-3 w-16" />
    </div>
  {:else}
    <!-- Actual value display -->
    <div
      class="font-bold rounded-lg {sizeClasses} {getBGColor(
        rawBgMgdl,
        isStale
      )} {getBorderStyle(isDisconnected, isStale)} {isPulsing
        ? 'pulse-once'
        : ''}"
    >
      {displayValue}
    </div>

    {#if statusText}
      <Tooltip.Root>
        <Tooltip.Trigger>
          {#snippet child({ props })}
            <span
              {...props}
              class="text-xs cursor-help {isDisconnected
                ? 'text-destructive font-medium'
                : 'text-muted-foreground'}"
            >
              {statusText}
            </span>
          {/snippet}
        </Tooltip.Trigger>
        <Tooltip.Content side="bottom">
          <p>{statusTooltip || statusText}</p>
        </Tooltip.Content>
      </Tooltip.Root>
    {/if}
  {/if}
</div>

<style>
  @keyframes flash-border {
    0%,
    100% {
      opacity: 1;
    }
    50% {
      opacity: 0.3;
    }
  }

  .animate-flash-border {
    animation: flash-border 1.5s ease-in-out infinite;
  }

  @keyframes pulse-once {
    0% {
      transform: scale(1);
      filter: brightness(1);
    }
    50% {
      transform: scale(1.05);
      filter: brightness(1.15);
    }
    100% {
      transform: scale(1);
      filter: brightness(1);
    }
  }

  .pulse-once {
    animation: pulse-once 0.6s ease-in-out;
  }
</style>
