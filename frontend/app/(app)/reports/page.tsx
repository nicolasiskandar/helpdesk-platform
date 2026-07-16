"use client"

import { FileDown, FileSpreadsheet, Timer, Gauge, TrendingUp, Star } from "lucide-react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { StatCard } from "@/components/stat-card"
import { VolumeChart } from "@/components/reports/volume-chart"
import { ResolutionChart } from "@/components/reports/resolution-chart"
import { useStore } from "@/lib/store"
import {
  ticketTrend,
  resolutionTimeTrend,
  agentPerformance,
} from "@/lib/analytics"

function initials(name: string) {
  return name
    .split(" ")
    .map((n) => n[0])
    .slice(0, 2)
    .join("")
}

export default function ReportsPage() {
  const { tickets } = useStore()
  const trend = ticketTrend()
  const resolution = resolutionTimeTrend()
  const performance = [...agentPerformance(tickets)].sort(
    (a, b) => b.resolved - a.resolved
  )

  function handleExport(kind: string) {
    toast.success(`${kind} export started`, {
      description: "Your report will be ready to download shortly.",
    })
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold tracking-tight">
            Reports & Analytics
          </h2>
          <p className="text-sm text-muted-foreground">
            Performance metrics for the last 6 months
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={() => handleExport("PDF")}>
            <FileDown data-icon="inline-start" />
            Export PDF
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => handleExport("Excel")}
          >
            <FileSpreadsheet data-icon="inline-start" />
            Export Excel
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard
          label="Avg. Resolution"
          value="6.8h"
          icon={Timer}
          accent="primary"
          trend={{ value: "0.3h", direction: "down", positive: true }}
          hint="faster"
        />
        <StatCard
          label="SLA Compliance"
          value="96.4%"
          icon={Gauge}
          accent="success"
          trend={{ value: "2.1%", direction: "up", positive: true }}
        />
        <StatCard
          label="Resolution Rate"
          value="94%"
          icon={TrendingUp}
          accent="info"
          hint="Tickets closed"
        />
        <StatCard
          label="CSAT Score"
          value="4.6/5"
          icon={Star}
          accent="warning"
          trend={{ value: "0.2", direction: "up", positive: true }}
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-5">
        <Card className="lg:col-span-3">
          <CardHeader>
            <CardTitle>Ticket Volume</CardTitle>
            <CardDescription>Created vs resolved per month</CardDescription>
          </CardHeader>
          <CardContent>
            <VolumeChart data={trend} />
          </CardContent>
        </Card>
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Avg. Resolution Time</CardTitle>
            <CardDescription>Hours to resolve, trending down</CardDescription>
          </CardHeader>
          <CardContent>
            <ResolutionChart data={resolution} />
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Agent Performance Report</CardTitle>
          <CardDescription>
            Resolution metrics per support agent this period
          </CardDescription>
        </CardHeader>
        <CardContent className="px-0">
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead>Agent</TableHead>
                  <TableHead className="text-right">Assigned</TableHead>
                  <TableHead className="text-right">Resolved</TableHead>
                  <TableHead className="text-right">Active</TableHead>
                  <TableHead className="text-right">Resolution Rate</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {performance.map((agent, i) => {
                  const rate =
                    agent.assigned > 0
                      ? Math.round((agent.resolved / agent.assigned) * 100)
                      : 0
                  return (
                    <TableRow key={agent.id}>
                      <TableCell>
                        <div className="flex items-center gap-2.5">
                          <Avatar className="size-7">
                            <AvatarFallback className="bg-muted text-[10px]">
                              {initials(agent.name)}
                            </AvatarFallback>
                          </Avatar>
                          <span className="font-medium">{agent.name}</span>
                          {i === 0 ? (
                            <Badge variant="secondary" className="gap-1">
                              <Star className="size-3" />
                              Top
                            </Badge>
                          ) : null}
                        </div>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {agent.assigned}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {agent.resolved}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {agent.active}
                      </TableCell>
                      <TableCell className="text-right font-medium tabular-nums">
                        {rate}%
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
