"use client"

import { Line, LineChart, CartesianGrid, XAxis, YAxis } from "recharts"
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart"

const config = {
  hours: { label: "Avg. Resolution (hrs)", color: "var(--chart-2)" },
} satisfies ChartConfig

export function ResolutionChart({
  data,
}: {
  data: { month: string; hours: number }[]
}) {
  return (
    <ChartContainer config={config} className="h-[280px] w-full">
      <LineChart data={data} margin={{ left: 4, right: 12, top: 8 }}>
        <CartesianGrid vertical={false} strokeDasharray="3 3" />
        <XAxis
          dataKey="month"
          tickLine={false}
          axisLine={false}
          tickMargin={8}
        />
        <YAxis tickLine={false} axisLine={false} width={32} domain={[0, "auto"]} />
        <ChartTooltip content={<ChartTooltipContent />} />
        <Line
          dataKey="hours"
          type="monotone"
          stroke="var(--color-hours)"
          strokeWidth={2}
          dot={{ r: 3, fill: "var(--color-hours)" }}
          activeDot={{ r: 5 }}
        />
      </LineChart>
    </ChartContainer>
  )
}
