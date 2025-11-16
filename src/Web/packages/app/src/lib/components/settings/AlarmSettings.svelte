<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
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
  import { alarmMinutesOptions } from "./constants.js";
  import type { ClientSettings } from "$lib/stores/serverSettings.js";
  import type { AlarmMinuteType } from "./types.js";

  interface Props {
    settings: ClientSettings;
  }

  let { settings }: Props = $props();

  function addAlarmMinute(alarmType: AlarmMinuteType, minutes: number) {
    const current = settings[alarmType] || [];
    if (!current.includes(minutes)) {
      settings[alarmType] = [...current, minutes].sort((a, b) => a - b);
    }
  }

  function removeAlarmMinute(alarmType: AlarmMinuteType, minutes: number) {
    const current = settings[alarmType] || [];
    settings[alarmType] = current.filter((m) => m !== minutes);
  }
</script>

<Card class="settings-section">
  <CardHeader>
    <CardTitle>BG Alarms</CardTitle>
  </CardHeader>
  <CardContent class="space-y-4">
    <div class="flex items-center space-x-2">
      <Checkbox id="alarmUrgentHigh" bind:checked={settings.alarmUrgentHigh} />
      <Label for="alarmUrgentHigh">Urgent High Alarm</Label>
    </div>
    {#if settings.alarmUrgentHigh}
      <div class="ml-6 space-y-2">
        <Label class="text-sm text-muted-foreground">
          Snooze options (minutes):
        </Label>
        <div class="flex flex-wrap gap-2 mb-2">
          {#each settings.alarmUrgentHighMins || [] as minutes}
            <span
              class="bg-blue-50 text-blue-700 px-2 py-1 rounded-xl text-xs flex items-center gap-1"
            >
              {minutes}
              <Button
                variant="ghost"
                size="sm"
                onclick={() =>
                  removeAlarmMinute("alarmUrgentHighMins", minutes)}
              >
                ×
              </Button>
            </span>
          {/each}
        </div>
        <Select
          type="single"
          onValueChange={(value) =>
            addAlarmMinute("alarmUrgentHighMins", parseInt(value || "0"))}
        >
          <SelectTrigger>
            <span class="text-muted-foreground">Add snooze option</span>
          </SelectTrigger>
          <SelectContent>
            {#each alarmMinutesOptions as option}
              <SelectItem value={option.value.toString()}>
                {option.label}
              </SelectItem>
            {/each}
          </SelectContent>
        </Select>
      </div>
    {/if}

    <div class="flex items-center space-x-2">
      <Checkbox id="alarmHigh" bind:checked={settings.alarmHigh} />
      <Label for="alarmHigh">High Alarm</Label>
    </div>
    {#if settings.alarmHigh}
      <div class="ml-6 space-y-2">
        <Label class="text-sm text-muted-foreground">
          Snooze options (minutes):
        </Label>
        <div class="flex flex-wrap gap-2 mb-2">
          {#each settings.alarmHighMins || [] as minutes}
            <span
              class="bg-blue-50 text-blue-700 px-2 py-1 rounded-xl text-xs flex items-center gap-1"
            >
              {minutes}
              <Button
                variant="ghost"
                size="sm"
                onclick={() => removeAlarmMinute("alarmHighMins", minutes)}
              >
                ×
              </Button>
            </span>
          {/each}
        </div>
        <Select
          type="single"
          onValueChange={(value) =>
            addAlarmMinute("alarmHighMins", parseInt(value || "0"))}
        >
          <SelectTrigger>
            <span class="text-muted-foreground">Add snooze option</span>
          </SelectTrigger>
          <SelectContent>
            {#each alarmMinutesOptions as option}
              <SelectItem value={option.value.toString()}>
                {option.label}
              </SelectItem>
            {/each}
          </SelectContent>
        </Select>
      </div>
    {/if}

    <div class="flex items-center space-x-2">
      <Checkbox id="alarmLow" bind:checked={settings.alarmLow} />
      <Label for="alarmLow">Low Alarm</Label>
    </div>
    {#if settings.alarmLow}
      <div class="ml-6 space-y-2">
        <Label class="text-sm text-muted-foreground">
          Snooze options (minutes):
        </Label>
        <div class="flex flex-wrap gap-2 mb-2">
          {#each settings.alarmLowMins || [] as minutes}
            <span
              class="bg-blue-50 text-blue-700 px-2 py-1 rounded-xl text-xs flex items-center gap-1"
            >
              {minutes}
              <Button
                variant="ghost"
                size="sm"
                onclick={() => removeAlarmMinute("alarmLowMins", minutes)}
              >
                ×
              </Button>
            </span>
          {/each}
        </div>
        <Select
          type="single"
          onValueChange={(value) =>
            addAlarmMinute("alarmLowMins", parseInt(value || "0"))}
        >
          <SelectTrigger>
            <span class="text-muted-foreground">Add snooze option</span>
          </SelectTrigger>
          <SelectContent>
            {#each alarmMinutesOptions as option}
              <SelectItem value={option.value.toString()}>
                {option.label}
              </SelectItem>
            {/each}
          </SelectContent>
        </Select>
      </div>
    {/if}

    <div class="flex items-center space-x-2">
      <Checkbox id="alarmUrgentLow" bind:checked={settings.alarmUrgentLow} />
      <Label for="alarmUrgentLow">Urgent Low Alarm</Label>
    </div>
    {#if settings.alarmUrgentLow}
      <div class="ml-6 space-y-2">
        <Label class="text-sm text-muted-foreground">
          Snooze options (minutes):
        </Label>
        <div class="flex flex-wrap gap-2 mb-2">
          {#each settings.alarmUrgentLowMins || [] as minutes}
            <span
              class="bg-blue-50 text-blue-700 px-2 py-1 rounded-xl text-xs flex items-center gap-1"
            >
              {minutes}
              <Button
                variant="ghost"
                size="sm"
                onclick={() => removeAlarmMinute("alarmUrgentLowMins", minutes)}
              >
                ×
              </Button>
            </span>
          {/each}
        </div>
        <Select
          type="single"
          onValueChange={(value) =>
            addAlarmMinute("alarmUrgentLowMins", parseInt(value || "0"))}
        >
          <SelectTrigger>
            <span class="text-muted-foreground">Add snooze option</span>
          </SelectTrigger>
          <SelectContent>
            {#each alarmMinutesOptions as option}
              <SelectItem value={option.value.toString()}>
                {option.label}
              </SelectItem>
            {/each}
          </SelectContent>
        </Select>
      </div>
    {/if}
  </CardContent>
</Card>

<Card class="settings-section">
  <CardHeader>
    <CardTitle>Data Staleness Alarms</CardTitle>
  </CardHeader>
  <CardContent class="space-y-4">
    <div class="flex items-center space-x-2">
      <Checkbox
        id="alarmTimeagoWarn"
        bind:checked={settings.alarmTimeagoWarn}
      />
      <Label for="alarmTimeagoWarn">Data Warning Alarm</Label>
    </div>
    {#if settings.alarmTimeagoWarn}
      <div class="ml-6 flex items-center space-x-3">
        <Label for="warnAfter" class="min-w-fit">Warning after:</Label>
        <Input
          id="warnAfter"
          type="number"
          bind:value={settings.alarmTimeagoWarnMins}
          min="1"
          max="60"
          class="w-20"
        />
        <span class="text-sm text-muted-foreground">minutes</span>
      </div>
    {/if}

    <div class="flex items-center space-x-2">
      <Checkbox
        id="alarmTimeagoUrgent"
        bind:checked={settings.alarmTimeagoUrgent}
      />
      <Label for="alarmTimeagoUrgent">Data Urgent Alarm</Label>
    </div>
    {#if settings.alarmTimeagoUrgent}
      <div class="ml-6 flex items-center space-x-3">
        <Label for="urgentAfter" class="min-w-fit">Urgent after:</Label>
        <Input
          id="urgentAfter"
          type="number"
          bind:value={settings.alarmTimeagoUrgentMins}
          min="1"
          max="120"
          class="w-20"
        />
        <span class="text-sm text-muted-foreground">minutes</span>
      </div>
    {/if}
  </CardContent>
</Card>
