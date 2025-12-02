<script lang="ts">
  import { Badge } from "$lib/components/ui/badge";
  import * as Tabs from "$lib/components/ui/tabs";
  import {
    TREATMENT_CATEGORIES,
    type TreatmentCategoryId,
  } from "$lib/constants/treatment-categories";
  import {
    Syringe,
    Activity,
    Utensils,
    Smartphone,
    FileText,
    List,
  } from "lucide-svelte";

  interface Props {
    activeCategory: TreatmentCategoryId | "all";
    categoryCounts: Record<string, number>;
    onChange: (category: TreatmentCategoryId | "all") => void;
  }

  let { activeCategory, categoryCounts, onChange }: Props = $props();

  // Icon mapping
  const iconMap = {
    bolus: Syringe,
    basal: Activity,
    carbs: Utensils,
    device: Smartphone,
    notes: FileText,
  } as const;

  function getTotalCount(): number {
    return Object.values(categoryCounts).reduce((sum, count) => sum + count, 0);
  }
</script>

<Tabs.Root
  value={activeCategory}
  onValueChange={(v) => onChange(v as TreatmentCategoryId | "all")}
>
  <Tabs.List
    class="grid w-full grid-cols-3 lg:grid-cols-6 h-auto gap-2 bg-transparent p-0"
  >
    <!-- All tab -->
    <Tabs.Trigger
      value="all"
      class="flex flex-col items-center gap-1 p-3 data-[state=active]:bg-primary/10 data-[state=active]:text-primary rounded-lg border data-[state=active]:border-primary/30"
    >
      <List class="h-5 w-5" />
      <span class="text-xs font-medium">All</span>
      <Badge variant="secondary" class="text-[10px] px-1.5 py-0">
        {getTotalCount()}
      </Badge>
    </Tabs.Trigger>

    <!-- Category tabs -->
    {#each Object.entries(TREATMENT_CATEGORIES) as [id, category]}
      {@const Icon = iconMap[id as keyof typeof iconMap]}
      {@const count = categoryCounts[id] || 0}
      <Tabs.Trigger
        value={id}
        class="flex flex-col items-center gap-1 p-3 data-[state=active]:bg-primary/10 data-[state=active]:text-primary rounded-lg border data-[state=active]:border-primary/30"
      >
        <Icon class="h-5 w-5 {category.colorClass}" />
        <span class="text-xs font-medium truncate max-w-full">
          {category.name.split(" ")[0]}
        </span>
        <Badge variant="secondary" class="text-[10px] px-1.5 py-0">
          {count}
        </Badge>
      </Tabs.Trigger>
    {/each}
  </Tabs.List>
</Tabs.Root>
