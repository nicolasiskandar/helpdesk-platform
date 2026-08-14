"use client"

import * as React from "react"
import { FileDown, FileSpreadsheet, Timer, Gauge, TrendingUp, Ticket, Star } from "lucide-react"
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
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
import { RoleGuard } from "@/components/role-guard"
import { agentPerformance } from "@/lib/analytics"
import { apiGetStatistics } from "@/lib/api"
import type { AnalyticsResponse } from "@/lib/api"
import { exportReportExcel, exportReportPdf } from "@/lib/export"

function initials(name: string) {
  return name.slice(0, 2).toUpperCase()
}

function formatHours(hours: number | null | undefined): string {
  if (hours == null || hours < 0) return "—"
  return `${hours}h`
}

const RANGE_OPTIONS = [
  { value: 1, label: "1M", text: "last month" },
  { value: 6, label: "6M", text: "last 6 months" },
  { value: 12, label: "1Y", text: "last 12 months" },
  { value: 0, label: "All", text: "all time" },
] as const

function rangeText(months: number): string {
  return RANGE_OPTIONS.find((r) => r.value === months)?.text ?? "this period"
}

export default function ReportsPage() {
  const { tickets, userMap } = useStore()
  const [stats, setStats] = React.useState<AnalyticsResponse | null>(null)
  const [months, setMonths] = React.useState<number>(6)
  const [loading, setLoading] = React.useState(true)
  const [error, setError] = React.useState<string | null>(null)

  React.useEffect(() => {
    let cancelled = false
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const data = await apiGetStatistics(months)
        if (!cancelled) setStats(data)
      } catch (err: any) {
        if (!cancelled) setError(err?.message || "Failed to load analytics.")
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    load()
    return () => {
      cancelled = true
    }
  }, [months])

  const performance = [...agentPerformance(tickets, userMap)].sort(
    (a, b) => b.resolved - a.resolved
  )

  const overview = stats?.overview
  const volume = stats?.volumeTrend ?? []
  const resolution = (stats?.resolutionTrend ?? []).map((t) => ({
    month: t.month,
    hours: t.averageHours,
  }))

  function handleExport(kind: "pdf" | "excel") {
    if (!stats) return
    if (kind === "pdf") {
      exportReportPdf(stats, performance)
    } else {
      exportReportExcel(stats, performance)
    }
    toast.success(`${kind === "pdf" ? "PDF" : "Excel"} export ready`)
  }

  return (
    <RoleGuard allowedRoles={["admin", "manager"]}>
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold tracking-tight">
            Reports & Analytics
          </h2>
          <p className="text-sm text-muted-foreground">
            Performance metrics for {rangeText(months)}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Select
            value={String(months)}
            onValueChange={(v) => {
              if (v != null) setMonths(Number(v))
            }}
          >
            <SelectTrigger size="sm" className="min-w-28">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {RANGE_OPTIONS.map((r) => (
                <SelectItem key={r.value} value={String(r.value)}>
                  {r.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            variant="outline"
            size="sm"
            disabled={!stats}
            onClick={() => handleExport("pdf")}
          >
            <FileDown data-icon="inline-start" />
            Export PDF
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={!stats}
            onClick={() => handleExport("excel")}
          >
            <FileSpreadsheet data-icon="inline-start" />
            Export Excel
          </Button>
        </div>
      </div>

      {error ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 p-8 text-center">
            <p className="text-sm text-destructive">{error}</p>
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                setStats(null)
                setLoading(true)
                apiGetStatistics(months)
                  .then((data) => setStats(data))
                  .catch((err: any) => setError(err?.message || "Failed to load analytics."))
                  .finally(() => setLoading(false))
              }}
            >
              Retry
            </Button>
          </CardContent>
        </Card>
      ) : loading && !stats ? (
        <Card>
          <CardContent className="flex items-center justify-center p-10 text-sm text-muted-foreground">
            Loading analytics...
          </CardContent>
        </Card>
      ) : (
        <>
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard
          label="Avg. Resolution"
          value={formatHours(overview?.averageResolutionHours)}
          icon={Timer}
          accent="primary"
          hint="Avg. hours to resolve"
        />
        <StatCard
          label="SLA Compliance"
          value={
            overview?.slaCompliance != null
              ? `${overview.slaCompliance}%`
              : "—"
          }
          icon={Gauge}
          accent="success"
          hint="Within priority SLA"
        />
        <StatCard
          label="Resolution Rate"
          value={
            overview?.resolutionRate != null
              ? `${overview.resolutionRate}%`
              : "—"
          }
          icon={TrendingUp}
          accent="info"
          hint="Tickets resolved"
        />
        <StatCard
          label="Total Tickets"
          value={overview?.total ?? "—"}
          icon={Ticket}
          accent="warning"
          hint={`In ${rangeText(months)}`}
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-5">
        <Card className="lg:col-span-3">
          <CardHeader>
            <CardTitle>Ticket Volume</CardTitle>
            <CardDescription>Created vs resolved per month</CardDescription>
          </CardHeader>
          <CardContent>
            <VolumeChart data={volume} />
          </CardContent>
        </Card>
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Avg. Resolution Time</CardTitle>
            <CardDescription>Hours to resolve, per month</CardDescription>
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
            Resolution metrics per support agent — based on the loaded ticket
            list, not the selected range
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
        </>
      )}
    </div>
    </RoleGuard>
  )
}
