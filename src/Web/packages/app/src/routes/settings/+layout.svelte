<script lang="ts">
  import { page } from "$app/state";
  import { Button } from "$lib/components/ui/button";
  import { createSettingsStore } from "$lib/stores/settings-store.svelte";
  import {
    Smartphone,
    Syringe,
    Brain,
    Sparkles,
    Bell,
    Plug,
    HeartHandshake,
    Settings,
    ChevronLeft,
  } from "lucide-svelte";

  const { children } = $props();

  // Create settings store in context - this will auto-load and be available to all child pages
  // The store instance is referenced by child components via getSettingsStore()
  createSettingsStore();

  type NavItem = {
    title: string;
    href: string;
    icon: typeof Settings;
  };

  const settingsNav: NavItem[] = [
    { title: "Overview", href: "/settings", icon: Settings },
    { title: "Devices", href: "/settings/devices", icon: Smartphone },
    { title: "Therapy", href: "/settings/therapy", icon: Syringe },
    { title: "Algorithm", href: "/settings/algorithm", icon: Brain },
    { title: "Features", href: "/settings/features", icon: Sparkles },
    { title: "Notifications", href: "/settings/notifications", icon: Bell },
    { title: "Services", href: "/settings/services", icon: Plug },
    {
      title: "Support & Community",
      href: "/settings/support",
      icon: HeartHandshake,
    },
  ];

  const isActive = (href: string): boolean => {
    if (href === "/settings") {
      return page.url.pathname === "/settings";
    }
    return page.url.pathname.startsWith(href);
  };

  const isSubpage = $derived(page.url.pathname !== "/settings");
</script>

<div class="flex flex-col min-h-full">
  <!-- Breadcrumb navigation for subpages on mobile -->
  {#if isSubpage}
    <div class="md:hidden border-b border-border px-4 py-2">
      <Button variant="ghost" size="sm" href="/settings" class="gap-1 -ml-2">
        <ChevronLeft class="h-4 w-4" />
        Settings
      </Button>
    </div>
  {/if}

  <div class="flex flex-1">
    <!-- Desktop Sidebar Navigation -->
    <aside class="hidden md:flex w-64 shrink-0 border-r border-border">
      <nav class="flex flex-col gap-1 p-4 w-full">
        {#each settingsNav as item}
          <a
            href={item.href}
            class="flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors
              {isActive(item.href)
              ? 'bg-primary/10 text-primary font-medium'
              : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground'}"
          >
            <item.icon class="h-4 w-4" />
            {item.title}
          </a>
        {/each}
      </nav>
    </aside>

    <!-- Main Content -->
    <main class="flex-1 overflow-auto">
      {@render children()}
    </main>
  </div>
</div>
