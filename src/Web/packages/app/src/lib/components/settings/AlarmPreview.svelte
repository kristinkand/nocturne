<script lang="ts">
  import { onMount, onDestroy } from "svelte";
  import {
    previewAlarmSound,
    stopPreview,
    subscribeToPreviewState,
    type PreviewState,
  } from "$lib/audio/alarm-sounds";
  import type {
    AlarmProfileConfiguration,
    EmergencyContactConfig,
  } from "$lib/types/alarm-profile";
  import {
    Volume2,
    VolumeX,
    Square,
    Phone,
    Mail,
    AlertCircle,
  } from "lucide-svelte";
  import AlarmWaveform from "./AlarmWaveform.svelte";
  import { Button } from "$lib/components/ui/button";

  interface Props {
    profile: AlarmProfileConfiguration;
    isOpen?: boolean;
    emergencyContacts?: EmergencyContactConfig[];
  }

  let { profile, isOpen = false, emergencyContacts = [] }: Props = $props();

  let previewState = $state<PreviewState>({ isPlaying: false, soundId: null });
  let isFlashing = $state(false);
  let flashInterval: ReturnType<typeof setInterval> | null = null;
  let unsubscribe: (() => void) | null = null;

  // Get enabled emergency contacts
  let enabledContacts = $derived(
    emergencyContacts.filter((c) => c.enabled && (c.phone || c.email))
  );

  // Show emergency contacts when playing and option is enabled
  let showEmergencyOverlay = $derived(
    previewState.isPlaying &&
      profile.visual.showEmergencyContacts &&
      enabledContacts.length > 0
  );

  onMount(() => {
    unsubscribe = subscribeToPreviewState((state) => {
      previewState = state;

      // Handle screen flash visual
      if (state.isPlaying && profile.visual.screenFlash) {
        startFlashing();
      } else {
        stopFlashing();
      }
    });
  });

  onDestroy(() => {
    stopFlashing();
    unsubscribe?.();
    if (previewState.isPlaying) {
      stopPreview();
    }
  });

  // Stop preview when dialog closes
  $effect(() => {
    if (!isOpen && previewState.isPlaying) {
      stopPreview();
    }
  });

  function startFlashing() {
    if (flashInterval) return;

    isFlashing = true;
    flashInterval = setInterval(() => {
      isFlashing = !isFlashing;
    }, profile.visual.flashIntervalMs);
  }

  function stopFlashing() {
    if (flashInterval) {
      clearInterval(flashInterval);
      flashInterval = null;
    }
    isFlashing = false;
  }

  async function handlePreview() {
    if (previewState.isPlaying) {
      stopPreview();
      return;
    }

    await previewAlarmSound(profile.audio.soundId, {
      volume: profile.audio.maxVolume,
      ascending: profile.audio.ascendingVolume,
      startVolume: profile.audio.startVolume,
      ascendDurationSeconds: Math.min(profile.audio.ascendDurationSeconds, 5),
      vibrate: profile.vibration.enabled,
    });
  }
</script>

<!-- Screen flash overlay with emergency contacts -->
{#if isFlashing && profile.visual.screenFlash}
  <div
    class="fixed inset-0 z-100 pointer-events-none transition-opacity duration-100"
    style="background-color: {profile.visual.flashColor}; opacity: 0.3;"
  ></div>
{/if}

<!-- Emergency contacts overlay -->
{#if showEmergencyOverlay}
  <div
    class="fixed inset-0 z-101 flex items-center justify-center pointer-events-auto bg-black/60 backdrop-blur-sm"
  >
    <div
      class="bg-background border-2 border-destructive rounded-xl p-6 max-w-md mx-4 shadow-2xl"
    >
      <div class="flex items-center gap-3 mb-4">
        <div class="p-2 bg-destructive/10 rounded-full">
          <AlertCircle class="h-6 w-6 text-destructive" />
        </div>
        <div>
          <h2 class="text-lg font-bold text-destructive">
            IN CASE OF EMERGENCY
          </h2>
          <p class="text-sm text-muted-foreground">
            Contact the following people:
          </p>
        </div>
      </div>

      <div class="space-y-3 mb-6">
        {#each enabledContacts as contact}
          <div class="flex items-center gap-3 p-3 bg-muted/50 rounded-lg">
            <div class="flex-1">
              <p class="font-semibold">{contact.name || "Emergency Contact"}</p>
              <div class="flex flex-wrap gap-3 mt-1">
                {#if contact.phone}
                  <a
                    href="tel:{contact.phone}"
                    class="flex items-center gap-1 text-sm text-primary hover:underline"
                  >
                    <Phone class="h-3 w-3" />
                    {contact.phone}
                  </a>
                {/if}
                {#if contact.email}
                  <a
                    href="mailto:{contact.email}"
                    class="flex items-center gap-1 text-sm text-primary hover:underline"
                  >
                    <Mail class="h-3 w-3" />
                    {contact.email}
                  </a>
                {/if}
              </div>
            </div>
          </div>
        {/each}
      </div>

      <div class="flex gap-2">
        <Button
          class="flex-1"
          variant="destructive"
          onclick={() => stopPreview()}
        >
          Acknowledge Alarm
        </Button>
      </div>
    </div>
  </div>
{/if}

<div class="space-y-3">
  <!-- Waveform visualization -->
  <AlarmWaveform
    audioSettings={profile.audio}
    isPlaying={previewState.isPlaying}
    width={280}
    height={64}
  />

  <!-- Preview button -->
  <div class="flex items-center gap-3">
    <button
      type="button"
      class="flex items-center gap-2 px-4 py-2 rounded-lg border transition-all
        {previewState.isPlaying
        ? 'bg-primary text-primary-foreground border-primary'
        : 'bg-background hover:bg-muted border-input'}"
      onclick={handlePreview}
    >
      {#if previewState.isPlaying}
        <Square class="h-4 w-4 fill-current" />
        <span class="text-sm font-medium">Stop</span>
      {:else}
        <Volume2 class="h-4 w-4" />
        <span class="text-sm font-medium">Play Preview</span>
      {/if}
    </button>

    {#if profile.audio.ascendingVolume}
      <span class="text-xs text-muted-foreground">
        {profile.audio.startVolume}% → {profile.audio.maxVolume}% over {profile
          .audio.ascendDurationSeconds}s
      </span>
    {:else}
      <span class="text-xs text-muted-foreground">
        Volume: {profile.audio.maxVolume}%
      </span>
    {/if}
  </div>
</div>

{#if !profile.audio.enabled}
  <div class="flex items-center gap-2 text-muted-foreground text-sm mt-2">
    <VolumeX class="h-4 w-4" />
    <span>Sound disabled for this alarm</span>
  </div>
{/if}
