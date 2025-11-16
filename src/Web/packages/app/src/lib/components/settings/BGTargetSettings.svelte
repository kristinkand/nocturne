<script lang="ts">
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import type { ClientSettings } from "$lib/stores/serverSettings";

  interface Props {
    settings: ClientSettings;
  }

  let { settings }: Props = $props();
</script>

<Card class="settings-section">
  <CardHeader>
    <CardTitle>BG Target Range</CardTitle>
    <p class="text-sm text-muted-foreground">
      Set your target blood glucose range for better visualization.
    </p>
  </CardHeader>
  <CardContent class="space-y-4">
    <div class="flex items-center space-x-2">
      <Label for="bgHigh" class="min-w-[120px]">High Alert:</Label>
      <Input
        id="bgHigh"
        type="number"
        bind:value={settings.thresholds.high}
        min="100"
        max="400"
        step={settings.units === "mmol" ? "0.1" : "1"}
        class="flex-1"
      />
      <span class="text-sm text-muted-foreground min-w-[60px]">
        {settings.units}
      </span>
    </div>
    <div class="flex items-center space-x-2">
      <Label for="bgTargetTop" class="min-w-[120px]">Target Top:</Label>
      <Input
        id="bgTargetTop"
        type="number"
        bind:value={settings.thresholds.targetTop}
        min="80"
        max="300"
        step={settings.units === "mmol" ? "0.1" : "1"}
        class="flex-1"
      />
      <span class="text-sm text-muted-foreground min-w-[60px]">
        {settings.units}
      </span>
    </div>

    <div class="flex items-center space-x-2">
      <Label for="bgTargetBottom" class="min-w-[120px]">Target Bottom:</Label>
      <Input
        id="bgTargetBottom"
        type="number"
        bind:value={settings.thresholds.targetBottom}
        min="40"
        max="150"
        step={settings.units === "mmol" ? "0.1" : "1"}
        class="flex-1"
      />
      <span class="text-sm text-muted-foreground min-w-[60px]">
        {settings.units}
      </span>
    </div>

    <div class="flex items-center space-x-2">
      <Label for="bgLow" class="min-w-[120px]">Low Alert:</Label>
      <Input
        id="bgLow"
        type="number"
        bind:value={settings.thresholds.low}
        min="30"
        max="100"
        step={settings.units === "mmol" ? "0.1" : "1"}
        class="flex-1"
      />
      <span class="text-sm text-muted-foreground min-w-[60px]">
        {settings.units}
      </span>
    </div>
  </CardContent>
</Card>
