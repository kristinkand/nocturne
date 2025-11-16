<script lang="ts">
  import { Label } from "$lib/components/ui/label";
  import { Checkbox } from "$lib/components/ui/checkbox";
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { pluginOptions } from "./constants.js";
  import type { ClientSettings } from "$lib/stores/serverSettings.js";
  interface Props {
    settings: ClientSettings;
  }

  let { settings = $bindable() }: Props = $props();

  function togglePlugin(plugin: string) {
    const plugins = settings.showPlugins || [];
    const index = plugins.indexOf(plugin);

    if (index > -1) {
      settings.showPlugins = plugins.filter((p) => p !== plugin);
    } else {
      settings.showPlugins = [...plugins, plugin];
    }
  }

  function isPluginEnabled(plugin: string): boolean {
    return (settings.showPlugins || []).includes(plugin);
  }
</script>

<Card class="settings-section">
  <CardHeader>
    <CardTitle>Enable Plugins</CardTitle>
  </CardHeader>
  <CardContent>
    <p class="text-sm text-muted-foreground mb-4">
      Select which plugins to display in your Nightscout interface.
    </p>

    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      {#each pluginOptions as plugin}
        <div
          class="flex items-center space-x-2 p-3 border rounded-lg bg-muted/50"
        >
          <Checkbox
            id="plugin-{plugin.value}"
            checked={isPluginEnabled(plugin.value)}
            onCheckedChange={() => togglePlugin(plugin.value)}
          />
          <Label for="plugin-{plugin.value}" class="text-sm font-medium">
            {plugin.label}
          </Label>
        </div>
      {/each}
    </div>
  </CardContent>
</Card>
