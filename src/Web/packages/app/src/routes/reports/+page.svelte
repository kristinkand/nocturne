<script lang="ts">
  import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import { Separator } from "$lib/components/ui/separator";
  import {
    Activity,
    TrendingUp,
    Target,
    Settings,
    BarChart3,
    Clock,
    Calendar,
    PieChart,
    Zap,
    Search,
    FileText,
    Gauge,
    Heart,
    Brain,
    AlertTriangle,
    Stethoscope,
    Moon,
    Sun,
    Utensils,
    Dumbbell,
    Thermometer,
    Users,
    Shield,
    BookOpen,
    TrendingDown,
    Calculator,
    LineChart,
    ChartColumn,
  } from "lucide-svelte";
  import TIRStackedChart from "$lib/components/reports/TIRStackedChart.svelte";
  import { AmbulatoryGlucoseProfile } from "$lib/components/ambulatory-glucose-profile";
  import { GlucoseChart } from "$lib/components/glucose-chart";

  const tirMetrics = {};
  // Intent-based report categories
  const reportCategories = [
    {
      title: "How Am I Doing?",
      description:
        "Get a comprehensive overview of your current diabetes management status",
      icon: Activity,
      color: "text-blue-600",
      bgColor: "bg-blue-50",
      borderColor: "border-blue-200",
      reports: [
        {
          title: "Executive Summary",
          description:
            "HbA1c, TIR, variability score, and management quality index",
          href: "/reports/executive-summary",
          icon: Gauge,
          status: "available",
          tags: ["Essential", "Quick View", "HbA1c"],
        },
        {
          title: "Ambulatory Glucose Profile",
          description: "14-day glucose pattern overlay with percentile bands",
          href: "/reports/agp",
          icon: BarChart3,
          status: "available",
          tags: ["AGP", "Standard", "Clinical"],
        },
        {
          title: "Time in Range Analysis",
          description: "Detailed TIR breakdown with trend analysis and targets",
          href: "/reports/time-in-range",
          icon: Target,
          status: "available",
          tags: ["TIR", "Targets", "Progress"],
        },
        {
          title: "Glucose Variability",
          description: "CV, MAGE, J-Index, and glycemic variability metrics",
          href: "/reports/variability",
          icon: TrendingUp,
          status: "available",
          tags: ["Variability", "MAGE", "CV"],
        },
      ],
    },
    {
      title: "What's Working?",
      description:
        "Identify successful patterns and effective treatment strategies",
      icon: TrendingUp,
      color: "text-green-600",
      bgColor: "bg-green-50",
      borderColor: "border-green-200",
      reports: [
        {
          title: "Success Pattern Analysis",
          description:
            "AI-powered identification of your most effective strategies",
          href: "/reports/success-patterns",
          icon: Target,
          status: "coming-soon",
          tags: ["AI", "Patterns", "Success"],
        },
        {
          title: "Treatment Effectiveness",
          description:
            "Bolus timing, correction ratios, and treatment outcomes",
          href: "/reports/treatment-effectiveness",
          icon: Zap,
          status: "available",
          tags: ["Bolus", "Corrections", "IOB"],
        },
        {
          title: "Meal Response Analysis",
          description: "Pre/post meal patterns and optimal timing strategies",
          href: "/reports/meal-response",
          icon: Utensils,
          status: "coming-soon",
          tags: ["Meals", "Timing", "Response"],
        },
        {
          title: "Exercise Impact",
          description: "Activity correlation with glucose control and patterns",
          href: "/reports/exercise-impact",
          icon: Dumbbell,
          status: "coming-soon",
          tags: ["Exercise", "Activity", "Impact"],
        },
        {
          title: "Sleep Quality Correlation",
          description:
            "Sleep patterns impact on glucose control and variability",
          href: "/reports/sleep-correlation",
          icon: Moon,
          status: "coming-soon",
          tags: ["Sleep", "Overnight", "Quality"],
        },
      ],
    },
    {
      title: "Where Can I Improve?",
      description:
        "Discover patterns and areas for optimization in your management",
      icon: Search,
      color: "text-orange-600",
      bgColor: "bg-orange-50",
      borderColor: "border-orange-200",
      reports: [
        {
          title: "Pattern Recognition",
          description: "Hourly, daily, and weekly glucose pattern analysis",
          href: "/reports/hourly-stats",
          icon: Clock,
          status: "available",
          tags: ["Patterns", "Hourly", "Daily"],
        },
        {
          title: "Hypoglycemia Risk Analysis",
          description: "Analyze low glucose events and patterns to reduce risk",
          href: "/reports/hypo-risk",
          icon: AlertTriangle,
          status: "available",
          tags: ["Hypoglycemia", "Low", "Risk"],
        },
        {
          title: "Hyperglycemia Risk Analysis",
          description:
            "Analyze high glucose events and patterns to reduce risk",
          href: "/reports/hyper-risk",
          icon: TrendingUp,
          status: "available",
          tags: ["Hyperglycemia", "High", "Risk"],
        },
        {
          title: "Data Quality & Sensor Uptime",
          description: "Assess completeness and reliability of CGM data",
          href: "/reports/data-quality",
          icon: Shield,
          status: "available",
          tags: ["Data Quality", "Sensor", "Uptime"],
        },
        {
          title: "Correction Optimization",
          description: "Analyze correction doses and suggest improvements",
          href: "/reports/correction-optimization",
          icon: Calculator,
          status: "coming-soon",
          tags: ["Corrections", "Optimization", "Ratios"],
        },
        {
          title: "Dawn Phenomenon Analysis",
          description: "Early morning glucose rise patterns and mitigation",
          href: "/reports/dawn-phenomenon",
          icon: Sun,
          status: "coming-soon",
          tags: ["Dawn", "Morning", "Basal"],
        },
        {
          title: "Sensor Accuracy Tracking",
          description: "CGM accuracy analysis and calibration recommendations",
          href: "/reports/sensor-accuracy",
          icon: Target,
          status: "coming-soon",
          tags: ["CGM", "Accuracy", "Calibration"],
        },
      ],
    },
    {
      title: "Advanced Analytics",
      description:
        "Sophisticated analysis tools for deep insights and clinical use",
      icon: Settings,
      color: "text-purple-600",
      bgColor: "bg-purple-50",
      borderColor: "border-purple-200",
      reports: [
        {
          title: "Predictive Modeling",
          description: "Machine learning predictions for glucose trends",
          href: "/reports/predictive-modeling",
          icon: Brain,
          status: "coming-soon",
          tags: ["AI", "Prediction", "ML"],
        },
        {
          title: "Insulin Sensitivity Analysis",
          description: "ISF tracking, trends, and optimization suggestions",
          href: "/reports/insulin-sensitivity",
          icon: LineChart,
          status: "coming-soon",
          tags: ["ISF", "Sensitivity", "Trends"],
        },
        {
          title: "Carb Ratio Optimization",
          description: "I:C ratio analysis and personalized recommendations",
          href: "/reports/carb-ratio",
          icon: PieChart,
          status: "coming-soon",
          tags: ["I:C", "Carbs", "Ratios"],
        },
        {
          title: "Basal Rate Analysis",
          description: "Comprehensive basal rate testing and optimization",
          href: "/reports/basal-analysis",
          icon: BarChart3,
          status: "coming-soon",
          tags: ["Basal", "Testing", "Optimization"],
        },
        {
          title: "Comparative Analysis",
          description: "Compare periods, treatments, or lifestyle changes",
          href: "/reports/comparative-analysis",
          icon: TrendingDown,
          status: "coming-soon",
          tags: ["Compare", "A/B Testing", "Changes"],
        },
      ],
    },
    {
      title: "Clinical & Sharing",
      description:
        "Professional reports for healthcare providers and comprehensive documentation",
      icon: Stethoscope,
      color: "text-indigo-600",
      bgColor: "bg-indigo-50",
      borderColor: "border-indigo-200",
      reports: [
        {
          title: "Clinical Summary Report",
          description: "Professional summary for healthcare provider visits",
          href: "/reports/clinical-summary",
          icon: FileText,
          status: "coming-soon",
          tags: ["Clinical", "Provider", "Summary"],
        },
        {
          title: "Logbook Generator",
          description: "Traditional logbook format with customizable fields",
          href: "/reports/logbook",
          icon: BookOpen,
          status: "coming-soon",
          tags: ["Logbook", "Traditional", "Print"],
        },
        {
          title: "Insurance Documentation",
          description: "Reports formatted for insurance and prescription needs",
          href: "/reports/insurance-docs",
          icon: Shield,
          status: "coming-soon",
          tags: ["Insurance", "Documentation", "Prescriptions"],
        },
        {
          title: "Research Export",
          description: "Anonymized data export for research participation",
          href: "/reports/research-export",
          icon: FileText,
          status: "coming-soon",
          tags: ["Research", "Export", "Anonymized"],
        },
      ],
    },
    {
      title: "Lifestyle Integration",
      description:
        "Understand how life factors impact your diabetes management",
      icon: Heart,
      color: "text-teal-600",
      bgColor: "bg-teal-50",
      borderColor: "border-teal-200",
      reports: [
        {
          title: "Stress Impact Analysis",
          description: "Correlation between stress markers and glucose control",
          href: "/reports/stress-impact",
          icon: Thermometer,
          status: "coming-soon",
          tags: ["Stress", "Correlation", "Mental Health"],
        },
        {
          title: "Travel & Timezone Effects",
          description: "How travel and schedule changes affect your control",
          href: "/reports/travel-effects",
          icon: Clock,
          status: "coming-soon",
          tags: ["Travel", "Timezone", "Schedule"],
        },
        {
          title: "Seasonal Patterns",
          description: "Long-term seasonal trends and environmental factors",
          href: "/reports/seasonal-patterns",
          icon: Calendar,
          status: "coming-soon",
          tags: ["Seasonal", "Environment", "Long-term"],
        },
        {
          title: "Social Support Analysis",
          description: "How social interactions and support affect management",
          href: "/reports/social-support",
          icon: Users,
          status: "coming-soon",
          tags: ["Social", "Support", "Community"],
        },
      ],
    },
  ];

  let { data } = $props();
  $inspect(data.entries.length, "Entries Length");
</script>

<svelte:head>
  <title>Reports - Nocturne</title>
  <meta
    name="description"
    content="Comprehensive diabetes management analytics and insights"
  />
</svelte:head>

<div class="container mx-auto px-4 py-6 space-y-8">
  <!-- Header -->
  <div class="text-center space-y-4">
    <h1 class="text-4xl font-bold">Diabetes Analytics</h1>
    <p class="text-muted-foreground text-lg max-w-2xl mx-auto">
      Get insights that matter. Our intent-based reports answer your real
      questions about diabetes management.
    </p>
  </div>

  <!-- Key Metrics Dashboard -->
  {#await data.analysis then analysis}
    <Card class="border-2">
      <CardHeader>
        <CardTitle class="flex items-center gap-2">
          <Activity class="w-5 h-5" />
          Your Health at a Glance
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div class="grid grid-cols-2 md:grid-cols-4 gap-6">
          <div class="text-center">
            <div class="text-3xl font-bold text-red-600">
              {analysis?.glycemicVariability?.estimatedA1c?.toFixed(2) ?? "–"}%
            </div>
            <div class="text-sm text-muted-foreground">Estimated HbA1c</div>
          </div>
          <div class="text-center">
            <div class="text-3xl font-bold text-green-600">
              {analysis?.timeInRange?.percentages?.target?.toFixed(1) ?? "–"}%
            </div>
            <div class="text-sm text-muted-foreground">Time in Range</div>
          </div>
          <div class="text-center">
            <div class="text-3xl font-bold text-blue-600">
              {analysis?.basicStats?.median ?? "–"}
            </div>
            <div class="text-sm text-muted-foreground">Average (mg/dL)</div>
          </div>
          <div class="text-center">
            <div class="text-3xl font-bold text-purple-600">
              {Math.round(
                (new Date(analysis?.time?.end ?? 0).getTime() -
                  new Date(analysis?.time?.start ?? 0).getTime()) /
                  (1000 * 60 * 60 * 24)
              )}
            </div>
            <div class="text-sm text-muted-foreground">Days of Data</div>
          </div>
        </div>

        <!-- Time in Range Stacked Chart -->
        {#if tirMetrics}
          <div class="mt-8">
            <h3 class="text-lg font-semibold mb-4 flex items-center gap-2">
              <ChartColumn class="w-5 h-5" />
              Time in Range Breakdown
            </h3>
            <div class="h-72 md:h-96">
              <TIRStackedChart entries={data.entries} />
            </div>
            <div class="h-72 md:h-96">
              <AmbulatoryGlucoseProfile entries={data.entries} />
            </div>
            <div class="h-72 md:h-96">
              <GlucoseChart
                entries={data.entries}
                treatments={data.treatments}
                dateRange={data.dateRange}
              />
            </div>
          </div>
        {/if}

        <div class="text-xs text-muted-foreground text-center mt-4">
          Last updated: {new Date(
            data.dateRange.lastUpdated
          ).toLocaleDateString()}
        </div>
      </CardContent>
    </Card>
  {:catch error}
    <Card class="border-2 border-destructive">
      <CardHeader>
        <CardTitle class="flex items-center gap-2 text-destructive">
          <AlertTriangle class="w-5 h-5" />
          Error Loading Analytics
        </CardTitle>
      </CardHeader>
      <CardContent>
        <p class="text-destructive-foreground">
          There was an error generating your analytics report. This usually
          means there is not enough data in the selected time range to perform
          the necessary calculations.
        </p>
        <p class="text-sm text-muted-foreground mt-2">
          Please select a larger date range or ensure you have sufficient
          glucose readings.
        </p>
        <pre class="mt-4 p-2 bg-muted rounded-md text-xs overflow-auto">
            {error.message}
          </pre>
      </CardContent>
    </Card>
  {/await}

  <!-- Intent-Based Report Categories -->
  <div class="space-y-8">
    {#each reportCategories as category}
      {@const CategoryIcon = category.icon}
      <Card class="border-2 {category.borderColor}">
        <CardHeader class={category.bgColor}>
          <CardTitle class="flex items-center gap-3 text-xl">
            <CategoryIcon class="w-6 h-6 {category.color}" />
            {category.title}
          </CardTitle>
          <CardDescription class="text-base">
            {category.description}
          </CardDescription>
        </CardHeader>
        <CardContent class="pt-6">
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {#each category.reports as report}
              {@const ReportIcon = report.icon}
              <Card class="hover:shadow-md transition-all hover:scale-[1.02]">
                <CardHeader class="pb-3">
                  <div class="flex items-start justify-between mb-2">
                    <ReportIcon class="w-5 h-5 {category.color}" />
                    <Badge
                      variant={report.status === "available"
                        ? "default"
                        : "secondary"}
                    >
                      {report.status === "available"
                        ? "Available"
                        : "Coming Soon"}
                    </Badge>
                  </div>
                  <CardTitle class="text-base">{report.title}</CardTitle>
                  <CardDescription class="text-sm">
                    {report.description}
                  </CardDescription>
                </CardHeader>
                <CardContent class="pt-0">
                  <div class="flex flex-wrap gap-1 mb-3">
                    {#each report.tags as tag}
                      <Badge variant="outline" class="text-xs">{tag}</Badge>
                    {/each}
                  </div>
                  {#if report.status === "available"}
                    <Button href={report.href} size="sm" class="w-full">
                      View Report
                    </Button>
                  {:else}
                    <Button
                      disabled
                      size="sm"
                      variant="secondary"
                      class="w-full"
                    >
                      Coming Soon
                    </Button>
                  {/if}
                </CardContent>
              </Card>
            {/each}
          </div>
        </CardContent>
      </Card>
    {/each}
  </div>

  <Separator class="my-8" />

  <!-- Quick Actions -->
  <Card>
    <CardHeader>
      <CardTitle class="flex items-center gap-2">
        <Zap class="w-5 h-5" />
        Quick Actions
      </CardTitle>
    </CardHeader>
    <CardContent>
      <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
        <Button
          href="/reports/executive-summary"
          class="flex items-center gap-2"
        >
          <Gauge class="w-4 h-4" />
          Overview
        </Button>
        <Button
          href="/reports/hourly-stats"
          variant="secondary"
          class="flex items-center gap-2"
        >
          <Clock class="w-4 h-4" />
          Patterns
        </Button>
        <Button
          href="/reports/treatment-effectiveness"
          variant="secondary"
          class="flex items-center gap-2"
        >
          <FileText class="w-4 h-4" />
          Treatments
        </Button>
        <Button href="/" variant="outline" class="flex items-center gap-2">
          <Activity class="w-4 h-4" />
          Dashboard
        </Button>
      </div>
    </CardContent>
  </Card>

  <!-- Getting Started Guide -->
  <Card class="bg-gradient-to-r from-blue-50 to-purple-50 border-blue-200">
    <CardHeader>
      <CardTitle class="flex items-center gap-2">
        <Search class="w-5 h-5 text-blue-600" />
        New to Reports? Start Here
      </CardTitle>
    </CardHeader>
    <CardContent>
      <div class="grid grid-cols-1 md:grid-cols-3 gap-6 text-sm">
        <div>
          <h3 class="font-semibold mb-2 text-blue-700">1. Check Your Status</h3>
          <p class="text-muted-foreground mb-2">
            Start with "How Am I Doing?" to see your overall diabetes management
            performance.
          </p>
          <Button href="/reports/executive-summary" size="sm" variant="outline">
            View Overview
          </Button>
        </div>
        <div>
          <h3 class="font-semibold mb-2 text-green-700">2. Find What Works</h3>
          <p class="text-muted-foreground mb-2">
            Explore "What's Working?" to identify your most successful
            management strategies.
          </p>
          <Button
            href="/reports/treatment-effectiveness"
            size="sm"
            variant="outline"
          >
            Analyze Treatments
          </Button>
        </div>
        <div>
          <h3 class="font-semibold mb-2 text-orange-700">
            3. Optimize Performance
          </h3>
          <p class="text-muted-foreground mb-2">
            Use "Where Can I Improve?" to discover patterns and optimization
            opportunities.
          </p>
          <Button href="/reports/hourly-stats" size="sm" variant="outline">
            Find Patterns
          </Button>
        </div>
      </div>
    </CardContent>
  </Card>
</div>
