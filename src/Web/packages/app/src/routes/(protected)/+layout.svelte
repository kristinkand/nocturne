<script lang="ts">
  /**
   * Layout for protected routes
   * This layout receives user data from the server load function
   * and provides it to child routes
   */
  import type { LayoutData } from "./$types";

  const { data, children } = $props<{ data: LayoutData; children: any }>();
</script>

<!-- Pass through to child routes -->
{@render children()}
