"use client"

import { Bar, BarChart, XAxis, YAxis, CartesianGrid } from "recharts"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart"

const config = {
  count: { label: "Tickets", color: "var(--chart-1)" },
} satisfies ChartConfig

export function CategoryChart({
  data,
}: {
  data: { category: string; count: number }[]
}) {
  return (
    <Card className="h-full">
      <CardHeader>
        <CardTitle>Tickets by Category</CardTitle>
        <CardDescription>Distribution across support categories</CardDescription>
      </CardHeader>
      <CardContent>
        <ChartContainer config={config} className="h-[240px] w-full">
          <BarChart
            data={data}
            layout="vertical"
            margin={{ left: 8, right: 16 }}
          >
            <CartesianGrid horizontal={false} strokeDasharray="3 3" />
            <YAxis
              type="category"
              dataKey="category"
              tickLine={false}
              axisLine={false}
              width={96}
              tick={{ fontSize: 12 }}
            />
            <XAxis type="number" hide />
            <ChartTooltip content={<ChartTooltipContent />} />
            <Bar dataKey="count" fill="var(--color-count)" radius={5} barSize={18} />
          </BarChart>
        </ChartContainer>
      </CardContent>
    </Card>
  )
}
