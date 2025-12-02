<script lang="ts">
  /**
   * Alarm Waveform Visualization Component Adapted from svelte-audio-waveform
   * (MIT License) https://github.com/Catsvilles/svelte-audio-waveform
   *
   * Displays actual waveform peaks for alarm sounds with volume envelope
   * visualization
   */
  import { onMount, onDestroy } from "svelte";
  import {
    subscribeToPreviewState,
    isCustomSound,
    getCustomSound,
    type PreviewState,
  } from "$lib/audio/alarm-sounds";
  import type { AlarmAudioSettings } from "$lib/types/alarm-profile";

  interface Props {
    audioSettings: AlarmAudioSettings;
    isPlaying?: boolean;
    width?: number;
    height?: number;
    color?: string;
    progressColor?: string;
    barWidth?: number;
  }

  let {
    audioSettings,
    isPlaying = false,
    width = 200,
    height = 60,
    color = "hsl(var(--muted-foreground) / 0.4)",
    progressColor = "hsl(var(--primary))",
    barWidth = 3,
  }: Props = $props();

  let canvasEl: HTMLCanvasElement | undefined = $state();
  let progressCanvasEl: HTMLCanvasElement | undefined = $state();
  let animationFrame: number | null = null;
  let startTime: number = 0;
  let previewState = $state<PreviewState>({ isPlaying: false, soundId: null });
  let unsubscribe: (() => void) | null = null;
  let pixelRatio = $state(1);
  let isLoadingPeaks = $state(false);

  // Computed: whether we're actively playing
  let playing = $derived(isPlaying || previewState.isPlaying);

  // Waveform peaks - actual audio data or generated for synthesized sounds
  let peaks = $state<number[]>([]);

  // Progress position (0-1)
  let progressPosition = $state(0);

  // In-memory cache for computed peaks (no persistence needed)
  let peaksCache: Map<string, number[]> = new Map();

  /** Generates an array of peak values from an AudioBuffer */
  function getPeaksFromBuffer(
    buffer: AudioBuffer,
    numberOfBuckets: number = 64,
    channel: number = 0
  ): number[] {
    if (channel >= buffer.numberOfChannels) {
      channel = 0;
    }

    const decodedAudioData = buffer.getChannelData(channel);
    const bucketDataSize = Math.floor(
      decodedAudioData.length / numberOfBuckets
    );
    const buckets: number[] = [];

    for (let i = 0; i < numberOfBuckets; i++) {
      const startingPoint = i * bucketDataSize;
      const endingPoint = startingPoint + bucketDataSize;

      let max = 0;
      for (let j = startingPoint; j < endingPoint; j++) {
        const absolute = Math.abs(decodedAudioData[j]);
        if (absolute > max) {
          max = absolute;
        }
      }

      buckets.push(max);
    }

    // Normalize peaks
    const maxPeak = Math.max(...buckets) || 1;
    return buckets.map((p) => p / maxPeak);
  }

  /** Load peaks from custom audio file */
  async function loadCustomSoundPeaks(soundId: string): Promise<number[]> {
    // Check cache first
    if (peaksCache.has(soundId)) {
      return peaksCache.get(soundId)!;
    }

    const sound = await getCustomSound(soundId);
    if (!sound) {
      return generateSynthesizedPeaks(soundId);
    }

    try {
      // Decode the audio data
      const audioContext = new AudioContext();

      // Convert data URL to array buffer
      const response = await fetch(sound.dataUrl);
      const arrayBuffer = await response.arrayBuffer();
      const audioBuffer = await audioContext.decodeAudioData(arrayBuffer);

      const extractedPeaks = getPeaksFromBuffer(audioBuffer, 64);

      // Cache the result in memory
      peaksCache.set(soundId, extractedPeaks);

      await audioContext.close();
      return extractedPeaks;
    } catch (err) {
      console.error("Failed to extract peaks from audio:", err);
      return generateSynthesizedPeaks(soundId);
    }
  }

  /**
   * Generate waveform peaks for synthesized (built-in) sounds These sounds are
   * generated via Web Audio API oscillators, so we create representative
   * waveform patterns based on their characteristics
   */
  function generateSynthesizedPeaks(
    soundId: string,
    numberOfBuckets: number = 64
  ): number[] {
    const generatedPeaks: number[] = [];

    // Different waveform patterns for different alarm types
    let pattern: (i: number, total: number) => number;

    if (soundId.includes("urgent") || soundId.includes("siren")) {
      // Aggressive, spiky waveform for urgent alarms
      pattern = (i, total) => {
        const t = i / total;
        const spike = Math.sin(t * Math.PI * 12) * 0.3;
        const base = 0.6 + Math.sin(t * Math.PI * 2) * 0.2;
        return Math.abs(base + spike);
      };
    } else if (soundId.includes("low")) {
      // Deeper, rounder waveform for low alerts
      pattern = (i, total) => {
        const t = i / total;
        return (
          0.4 +
          Math.sin(t * Math.PI * 3) * 0.25 +
          Math.sin(t * Math.PI * 7) * 0.15
        );
      };
    } else if (soundId.includes("high")) {
      // Rising pattern for high alerts
      pattern = (i, total) => {
        const t = i / total;
        const rise = t * 0.3;
        return 0.4 + rise + Math.sin(t * Math.PI * 5) * 0.2;
      };
    } else if (
      soundId.includes("chime") ||
      soundId.includes("soft") ||
      soundId.includes("bell")
    ) {
      // Gentle, smooth waveform
      pattern = (i, total) => {
        const t = i / total;
        const decay = Math.exp(-t * 2);
        return 0.3 + decay * 0.5 + Math.sin(t * Math.PI * 4) * 0.1 * decay;
      };
    } else if (soundId.includes("beep")) {
      // Square-ish beep pattern
      pattern = (i, total) => {
        const t = i / total;
        const beepPos = (t * 3) % 1;
        return beepPos < 0.4 ? 0.7 : 0.2;
      };
    } else {
      // Default balanced waveform
      pattern = (i, total) => {
        const t = i / total;
        return (
          0.5 +
          Math.sin(t * Math.PI * 6) * 0.25 +
          Math.sin(t * Math.PI * 13) * 0.1
        );
      };
    }

    for (let i = 0; i < numberOfBuckets; i++) {
      const value = pattern(i, numberOfBuckets);
      // Add slight variation
      const variation = Math.sin(i * 7.3) * 0.05;
      generatedPeaks.push(Math.max(0.15, Math.min(1, value + variation)));
    }

    return generatedPeaks;
  }

  /** Load peaks for the current sound (custom or synthesized) */
  async function loadPeaks(soundId: string): Promise<void> {
    isLoadingPeaks = true;

    try {
      if (isCustomSound(soundId)) {
        peaks = await loadCustomSoundPeaks(soundId);
      } else {
        peaks = generateSynthesizedPeaks(soundId);
      }
    } finally {
      isLoadingPeaks = false;
      updateCanvas();
    }
  }

  /** Find max absolute value in peaks array */
  function absMax(values: number[]): number {
    let max = 0;
    for (const v of values) {
      const abs = Math.abs(v);
      if (abs > max) max = abs;
    }
    return max || 1;
  }

  /** Draw waveform bars on canvas */
  function drawBars(
    ctx: CanvasRenderingContext2D,
    canvasWidth: number,
    canvasHeight: number,
    peakData: number[],
    fillColor: string,
    volumeScale: number = 1
  ) {
    const gap = Math.max(pixelRatio, 1);
    const bar = barWidth * pixelRatio;
    const step = bar + gap;
    const halfH = canvasHeight / 2;
    const maxVal = absMax(peakData);
    const scale = peakData.length / canvasWidth;

    ctx.fillStyle = fillColor;

    for (let i = 0; i < canvasWidth; i += step) {
      const peakIndex = Math.floor(i * scale);
      let h = Math.round((peakData[peakIndex] / maxVal) * halfH * volumeScale);
      if (h === 0) h = 1;

      // Draw bar from center
      const x = i;
      const y = halfH - h;
      const barHeight = h * 2;

      ctx.beginPath();
      ctx.roundRect(x, y, bar, barHeight, 1);
      ctx.fill();
    }
  }

  /** Draw wave (smooth line) on canvas */
  function drawWave(
    ctx: CanvasRenderingContext2D,
    canvasWidth: number,
    canvasHeight: number,
    peakData: number[],
    fillColor: string,
    volumeScale: number = 1
  ) {
    const halfH = canvasHeight / 2;
    const maxVal = absMax(peakData);
    const length = peakData.length;
    const scale = canvasWidth / length;

    ctx.fillStyle = fillColor;
    ctx.beginPath();
    ctx.moveTo(0, halfH);

    // Draw top half
    for (let i = 0; i < length; i++) {
      const h = Math.round((peakData[i] / maxVal) * halfH * volumeScale);
      ctx.lineTo(i * scale, halfH - h);
    }

    // Draw bottom half (mirror)
    for (let i = length - 1; i >= 0; i--) {
      const h = Math.round((peakData[i] / maxVal) * halfH * volumeScale);
      ctx.lineTo(i * scale, halfH + h);
    }

    ctx.closePath();
    ctx.fill();
  }

  /** Update canvas size and redraw */
  function updateCanvas() {
    if (!canvasEl || !progressCanvasEl || peaks.length === 0) return;

    const ctx = canvasEl.getContext("2d");
    const progressCtx = progressCanvasEl.getContext("2d");
    if (!ctx || !progressCtx) return;

    const canvasWidth = Math.round(width * pixelRatio);
    const canvasHeight = Math.round(height * pixelRatio);

    // Set canvas dimensions
    canvasEl.width = canvasWidth;
    canvasEl.height = canvasHeight;
    canvasEl.style.width = `${width}px`;
    canvasEl.style.height = `${height}px`;

    progressCanvasEl.width = canvasWidth;
    progressCanvasEl.height = canvasHeight;
    progressCanvasEl.style.width = `${width}px`;
    progressCanvasEl.style.height = `${height}px`;

    // Clear canvases
    ctx.clearRect(0, 0, canvasWidth, canvasHeight);
    progressCtx.clearRect(0, 0, canvasWidth, canvasHeight);

    // Calculate volume for visualization
    let volumeScale = playing ? 1 : 0.6;
    if (!playing && audioSettings.ascendingVolume) {
      volumeScale = (audioSettings.startVolume / 100) * 0.8;
    } else if (!playing) {
      volumeScale = (audioSettings.maxVolume / 100) * 0.6;
    }

    // Draw background waveform
    if (barWidth > 0) {
      drawBars(ctx, canvasWidth, canvasHeight, peaks, color, volumeScale);
    } else {
      drawWave(ctx, canvasWidth, canvasHeight, peaks, color, volumeScale);
    }

    // Draw progress waveform (clipped)
    if (playing && progressPosition > 0) {
      const progressWidth = Math.round(canvasWidth * progressPosition);
      progressCtx.save();
      progressCtx.beginPath();
      progressCtx.rect(0, 0, progressWidth, canvasHeight);
      progressCtx.clip();

      if (barWidth > 0) {
        drawBars(
          progressCtx,
          canvasWidth,
          canvasHeight,
          peaks,
          progressColor,
          1
        );
      } else {
        drawWave(
          progressCtx,
          canvasWidth,
          canvasHeight,
          peaks,
          progressColor,
          1
        );
      }

      progressCtx.restore();
    }

    // Draw ascending volume envelope if enabled
    if (audioSettings.ascendingVolume) {
      drawVolumeEnvelope(ctx, canvasWidth, canvasHeight);
    }

    // Draw volume indicator
    drawVolumeIndicator(ctx, canvasWidth, canvasHeight);
  }

  /** Draw volume envelope overlay */
  function drawVolumeEnvelope(
    ctx: CanvasRenderingContext2D,
    canvasWidth: number,
    canvasHeight: number
  ) {
    const startVol = audioSettings.startVolume / 100;
    const maxVol = audioSettings.maxVolume / 100;
    const halfH = canvasHeight / 2;
    const maxAmplitude = halfH - 4 * pixelRatio;

    ctx.save();
    ctx.globalAlpha = 0.1;
    ctx.fillStyle = progressColor;

    ctx.beginPath();
    ctx.moveTo(0, halfH);

    for (let x = 0; x < canvasWidth; x++) {
      const t = x / canvasWidth;
      const vol = startVol + (maxVol - startVol) * t;
      const amp = vol * maxAmplitude;
      ctx.lineTo(x, halfH - amp);
    }

    for (let x = canvasWidth - 1; x >= 0; x--) {
      const t = x / canvasWidth;
      const vol = startVol + (maxVol - startVol) * t;
      const amp = vol * maxAmplitude;
      ctx.lineTo(x, halfH + amp);
    }

    ctx.closePath();
    ctx.fill();
    ctx.restore();

    // Draw progress line if playing
    if (playing && progressPosition > 0 && progressPosition < 1) {
      const progressX = progressPosition * canvasWidth;
      ctx.save();
      ctx.strokeStyle = progressColor;
      ctx.lineWidth = 2 * pixelRatio;
      ctx.setLineDash([4 * pixelRatio, 2 * pixelRatio]);
      ctx.beginPath();
      ctx.moveTo(progressX, 4 * pixelRatio);
      ctx.lineTo(progressX, canvasHeight - 4 * pixelRatio);
      ctx.stroke();
      ctx.restore();
    }
  }

  /** Draw volume percentage indicator */
  function drawVolumeIndicator(
    ctx: CanvasRenderingContext2D,
    canvasWidth: number,
    _canvasHeight: number
  ) {
    let currentVolume = audioSettings.maxVolume;
    if (playing && audioSettings.ascendingVolume) {
      const elapsed = (performance.now() - startTime) / 1000;
      const progress = Math.min(
        elapsed / audioSettings.ascendDurationSeconds,
        1
      );
      currentVolume =
        audioSettings.startVolume +
        (audioSettings.maxVolume - audioSettings.startVolume) * progress;
    } else if (!playing && audioSettings.ascendingVolume) {
      currentVolume = audioSettings.startVolume;
    }

    ctx.save();
    ctx.font = `${10 * pixelRatio}px system-ui, sans-serif`;
    ctx.fillStyle = color;
    ctx.textAlign = "right";
    ctx.fillText(
      `${Math.round(currentVolume)}%`,
      canvasWidth - 4 * pixelRatio,
      12 * pixelRatio
    );

    if (audioSettings.ascendingVolume && !playing) {
      ctx.textAlign = "left";
      ctx.fillText("↗ Ascending", 4 * pixelRatio, 12 * pixelRatio);
    }
    ctx.restore();
  }

  /** Animation loop for playing state */
  function animate() {
    if (!playing) return;

    const elapsed = (performance.now() - startTime) / 1000;
    const duration = audioSettings.ascendingVolume
      ? Math.max(audioSettings.ascendDurationSeconds, 3)
      : 3;

    progressPosition = Math.min(elapsed / duration, 1);

    updateCanvas();

    if (progressPosition < 1) {
      animationFrame = requestAnimationFrame(animate);
    }
  }

  onMount(() => {
    pixelRatio = window.devicePixelRatio || 1;

    unsubscribe = subscribeToPreviewState((state) => {
      previewState = state;
      if (state.isPlaying && !startTime) {
        startTime = performance.now();
        progressPosition = 0;
      } else if (!state.isPlaying) {
        startTime = 0;
        progressPosition = 0;
      }
    });

    loadPeaks(audioSettings.soundId);
  });

  onDestroy(() => {
    unsubscribe?.();
    if (animationFrame) {
      cancelAnimationFrame(animationFrame);
    }
  });

  // Track previous sound ID to avoid unnecessary regeneration
  let prevSoundId = audioSettings.soundId;

  // Regenerate peaks when sound changes
  $effect(() => {
    const currentSoundId = audioSettings.soundId;
    if (currentSoundId !== prevSoundId) {
      prevSoundId = currentSoundId;
      loadPeaks(currentSoundId);
    }
  });

  // Track previous playing state
  let prevPlaying = false;

  // Handle playing state changes
  $effect(() => {
    const currentPlaying = playing;
    if (currentPlaying !== prevPlaying) {
      prevPlaying = currentPlaying;
      if (currentPlaying) {
        startTime = performance.now();
        progressPosition = 0;
        animate();
      } else {
        if (animationFrame) {
          cancelAnimationFrame(animationFrame);
          animationFrame = null;
        }
        progressPosition = 0;
        if (canvasEl) {
          updateCanvas();
        }
      }
    }
  });

  // Track previous settings for comparison
  let prevSettings = {
    ascendingVolume: audioSettings.ascendingVolume,
    startVolume: audioSettings.startVolume,
    maxVolume: audioSettings.maxVolume,
    ascendDurationSeconds: audioSettings.ascendDurationSeconds,
  };

  // Redraw when settings change
  $effect(() => {
    const current = {
      ascendingVolume: audioSettings.ascendingVolume,
      startVolume: audioSettings.startVolume,
      maxVolume: audioSettings.maxVolume,
      ascendDurationSeconds: audioSettings.ascendDurationSeconds,
    };

    if (
      current.ascendingVolume !== prevSettings.ascendingVolume ||
      current.startVolume !== prevSettings.startVolume ||
      current.maxVolume !== prevSettings.maxVolume ||
      current.ascendDurationSeconds !== prevSettings.ascendDurationSeconds
    ) {
      prevSettings = current;
      if (canvasEl && !playing) {
        updateCanvas();
      }
    }
  });

  // Handle resize
  function handleResize() {
    pixelRatio = window.devicePixelRatio || 1;
    updateCanvas();
  }
</script>

<svelte:window onresize={handleResize} />

<div
  class="waveform-container rounded-md overflow-hidden border bg-accent-foreground"
  style="position: relative; width: {width}px; height: {height}px;"
>
  <canvas
    bind:this={canvasEl}
    class="waveform-canvas"
    style="position: absolute; top: 0; left: 0;"
  ></canvas>
  <canvas
    bind:this={progressCanvasEl}
    class="progress-canvas"
    style="position: absolute; top: 0; left: 0; z-index: 1;"
  ></canvas>
</div>

<style>
  .waveform-container {
    display: block;
  }

  .waveform-canvas,
  .progress-canvas {
    display: block;
  }
</style>
