<script lang="ts">
  import { Label } from "$lib/components/ui/label";
  import { Checkbox } from "$lib/components/ui/checkbox";
  import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
  } from "$lib/components/ui/select";
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { themeOptions } from "./constants.js";
  import type { ClientSettings } from "$lib/stores/serverSettings.js";

  interface Props {
    settings: ClientSettings;
  }

  let { settings = $bindable() }: Props = $props();
</script>

<Card class="settings-section">
  <CardHeader>
    <CardTitle>Theme & Appearance</CardTitle>
  </CardHeader>
  <CardContent class="space-y-4">
    <div class="flex items-center space-x-3">
      <Label for="theme" class="min-w-fit">Theme:</Label>
      <Select type="single" bind:value={settings.theme}>
        <SelectTrigger>
          <span>
            {themeOptions.find((opt) => opt.value === settings.theme)?.label ||
              "Select theme"}
          </span>
        </SelectTrigger>
        <SelectContent>
          {#each themeOptions as option}
            <SelectItem value={option.value.toString()}>
              {option.label}
            </SelectItem>
          {/each}
        </SelectContent>
      </Select>
    </div>

    <div class="flex items-center space-x-2">
      <Checkbox id="nightMode" bind:checked={settings.nightMode} />
      <Label for="nightMode">Night Mode</Label>
    </div>

    <div class="flex items-center space-x-2">
      <Checkbox id="showForecast" bind:checked={settings.showForecast} />
      <Label for="showForecast">Show Forecast</Label>
    </div>
  </CardContent>
</Card>

<Card class="settings-section">
  <CardHeader>
    <CardTitle>Display Elements</CardTitle>
  </CardHeader>
  <CardContent class="space-y-4">
    <div class="flex items-center space-x-2">
      <Checkbox id="showBGON" bind:checked={settings.showBGON} />
      <Label for="showBGON">Show BG on Nightscout Icon</Label>
    </div>

    <div class="flex items-center space-x-2">
      <Checkbox id="showIOB" bind:checked={settings.showIOB} />
      <Label for="showIOB">Show IOB (Insulin on Board)</Label>
    </div>

    <div class="flex items-center space-x-2">
      <Checkbox id="showCOB" bind:checked={settings.showCOB} />
      <Label for="showCOB">Show COB (Carbs on Board)</Label>
    </div>

    <div class="flex items-center space-x-2">
      <Checkbox id="showBasal" bind:checked={settings.showBasal} />
      <Label for="showBasal">Show Basal Rate</Label>
    </div>
  </CardContent>
</Card>
