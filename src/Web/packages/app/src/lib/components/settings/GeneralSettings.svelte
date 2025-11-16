<script lang="ts">
  import { Label } from "$lib/components/ui/label";
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
  import {
    unitOptions,
    timeFormatOptions,
    languageOptions,
    focusHoursOptions,
  } from "./constants.js";
  import type { ClientSettings } from "$lib/stores/serverSettings.js";

  interface Props {
    settings: ClientSettings;
  }

  let { settings }: Props = $props();
</script>

<Card class="settings-section">
  <CardHeader>
    <CardTitle>Units & Format</CardTitle>
  </CardHeader>
  <CardContent class="space-y-4">
    <div class="input-row">
      <Label for="units">Blood Glucose Units:</Label>
      <Select type="single" bind:value={settings.units}>
        <SelectTrigger />
        <SelectContent>
          {#each unitOptions as option}
            <SelectItem value={option.value}>{option.label}</SelectItem>
          {/each}
        </SelectContent>
      </Select>
    </div>

    <div class="input-row">
      <Label for="timeFormat">Time Format:</Label>
      <Select type="single" bind:value={settings.timeFormat}>
        <SelectTrigger />
        <SelectContent>
          {#each timeFormatOptions as option}
            <SelectItem value={option.value.toString()}>
              {option.label}
            </SelectItem>
          {/each}
        </SelectContent>
      </Select>
    </div>

    <div class="input-row">
      <Label for="language">Language:</Label>
      <Select type="single" bind:value={settings.language}>
        <SelectTrigger />
        <SelectContent>
          {#each languageOptions as option}
            <SelectItem value={option.value}>{option.label}</SelectItem>
          {/each}
        </SelectContent>
      </Select>
    </div>

    <div class="input-row">
      <Label for="focusHours">Focus Hours:</Label>
      <Select bind:value={settings.focusHours}>
        <SelectTrigger />
        <SelectContent>
          {#each focusHoursOptions as option}
            <SelectItem value={option.value}>{option.label}</SelectItem>
          {/each}
        </SelectContent>
      </Select>
    </div>
  </CardContent>
</Card>
