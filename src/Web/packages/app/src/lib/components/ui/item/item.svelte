<script lang="ts" module>
  import { type VariantProps, tv } from "tailwind-variants";

  export const itemVariants = tv({
    base: "relative flex w-full items-center gap-3 rounded-lg p-3 text-left transition-colors",
    variants: {
      variant: {
        default: "bg-muted/40 border border-border/40",
        outline: "border border-border bg-transparent",
        muted: "bg-muted/20",
        ghost: "",
      },
      size: {
        default: "p-3",
        sm: "p-2 gap-2",
        lg: "p-4 gap-4",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  });

  export type ItemVariant = VariantProps<typeof itemVariants>["variant"];
  export type ItemSize = VariantProps<typeof itemVariants>["size"];
</script>

<script lang="ts">
  import { cn } from "$lib/utils";
  import type { Snippet } from "svelte";
  import type { HTMLAttributes } from "svelte/elements";

  let {
    ref = $bindable(null),
    class: className,
    variant = "default",
    size = "default",
    children,
    child,
    ...restProps
  }: HTMLAttributes<HTMLDivElement> & {
    ref?: HTMLDivElement | null;
    variant?: ItemVariant;
    size?: ItemSize;
    child?: Snippet<[{ props: Record<string, unknown> }]>;
  } = $props();

  const mergedClasses = $derived(
    cn(itemVariants({ variant, size }), className)
  );
</script>

{#if child}
  {@render child({ props: { class: mergedClasses, ...restProps } })}
{:else}
  <div bind:this={ref} class={mergedClasses} {...restProps}>
    {@render children?.()}
  </div>
{/if}
