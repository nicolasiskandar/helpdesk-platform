"use client"

import * as React from "react"
import { useStore, statusBadgeClass } from "@/lib/store"
import type { TicketStatus, TicketPriority, TicketCategory } from "@/lib/types"
import { TicketsTable } from "@/components/tickets-table"
import { Button } from "@/components/ui/button"
import { Card } from "@/components/ui/card"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  InputGroup,
  InputGroupInput,
  InputGroupAddon,
} from "@/components/ui/input-group"
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group"
import { Skeleton } from "@/components/ui/skeleton"
import { SearchIcon, PlusIcon, XIcon } from "lucide-react"
import Link from "next/link"

const STATUSES: TicketStatus[] = [
  "Open",
  "In Progress",
  "Pending",
  "Resolved",
  "Closed",
]
const PRIORITIES: TicketPriority[] = ["Critical", "High", "Medium", "Low"]
const CATEGORIES: TicketCategory[] = [
  "Hardware",
  "Software",
  "Network",
  "Access",
  "Other",
]

const DATE_RANGES = [
  { value: "hour", label: "Past Hour" },
  { value: "day", label: "Past Day" },
  { value: "week", label: "Past Week" },
  { value: "month", label: "Past Month" },
  { value: "custom", label: "Custom" },
] as const

const PRIORITY_ITEMS: Record<string, string> = {
  "": "All Priorities",
  Critical: "Critical",
  High: "High",
  Medium: "Medium",
  Low: "Low",
}

const CATEGORY_ITEMS: Record<string, string> = {
  "": "All Categories",
  Hardware: "Hardware",
  Software: "Software",
  Network: "Network",
  Access: "Access",
  Other: "Other",
}

const DATE_RANGE_ITEMS: Record<string, string> = {
  "": "All Time",
  hour: "Past Hour",
  day: "Past Day",
  week: "Past Week",
  month: "Past Month",
  custom: "Custom",
}

function getDateRangeBounds(range: string): { from: Date; to: Date } | null {
  const now = new Date()
  const to = now
  switch (range) {
    case "hour":
      return { from: new Date(now.getTime() - 60 * 60 * 1000), to }
    case "day":
      return { from: new Date(now.getTime() - 24 * 60 * 60 * 1000), to }
    case "week":
      return { from: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000), to }
    case "month":
      return { from: new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000), to }
    default:
      return null
  }
}

export default function TicketsPage() {
  const { tickets, role, ticketsLoading } = useStore()
  const [query, setQuery] = React.useState("")
  const [status, setStatus] = React.useState<string>("all")
  const [priority, setPriority] = React.useState("")
  const [category, setCategory] = React.useState("")
  const [dateRange, setDateRange] = React.useState("")
  const [customFrom, setCustomFrom] = React.useState("")
  const [customTo, setCustomTo] = React.useState("")

  const filtered = React.useMemo(() => {
    return tickets.filter((t) => {
      if (status !== "all" && t.status !== status) return false
      if (priority !== "" && t.priority !== priority) return false
      if (category !== "" && t.category !== category) return false

      const createdAt = new Date(t.createdAt)
      if (dateRange !== "" && dateRange !== "custom") {
        const bounds = getDateRangeBounds(dateRange)
        if (bounds && createdAt < bounds.from) return false
      }
      if (dateRange === "custom") {
        if (customFrom) {
          const fromDate = new Date(customFrom)
          fromDate.setHours(0, 0, 0, 0)
          if (createdAt < fromDate) return false
        }
        if (customTo) {
          const toDate = new Date(customTo)
          toDate.setHours(23, 59, 59, 999)
          if (createdAt > toDate) return false
        }
      }

      if (query) {
        const q = query.toLowerCase()
        const hit =
          t.subject.toLowerCase().includes(q) ||
          t.reference.toLowerCase().includes(q) ||
          t.description.toLowerCase().includes(q)
        if (!hit) return false
      }
      return true
    })
  }, [tickets, status, priority, category, query, dateRange, customFrom, customTo])

  const activeFilters =
    (status !== "all" ? 1 : 0) +
    (priority !== "" ? 1 : 0) +
    (category !== "" ? 1 : 0) +
    (dateRange !== "" ? 1 : 0)

  function clearFilters() {
    setStatus("all")
    setPriority("")
    setCategory("")
    setQuery("")
    setDateRange("")
    setCustomFrom("")
    setCustomTo("")
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold tracking-tight text-balance">
            Tickets
          </h1>
          <p className="text-sm text-muted-foreground">
            {role === "employee"
              ? "Track the status of your submitted requests."
              : "Search, filter, and manage the full ticket queue."}
          </p>
        </div>
        <Button
          render={<Link href="/tickets/new" />}
        >
          <PlusIcon data-icon="inline-start" />
          New Ticket
        </Button>
      </div>

      {/* Status quick filter */}
      <ToggleGroup
        value={status === "all" ? [] : [status]}
        onValueChange={(v: string[]) => setStatus(v[0] ?? "all")}
        variant="outline"
        className="flex-wrap justify-start"
      >
        {STATUSES.map((s) => (
          <ToggleGroupItem key={s} value={s} className="gap-2">
            <span
              className={`inline-block size-2 rounded-full border ${statusBadgeClass(
                s
              )}`}
              aria-hidden
            />
            {s}
          </ToggleGroupItem>
        ))}
      </ToggleGroup>

      <Card className="flex flex-col gap-4 p-4">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center">
          <InputGroup className="lg:max-w-xs">
            <InputGroupAddon>
              <SearchIcon />
            </InputGroupAddon>
            <InputGroupInput
              placeholder="Search subject or reference..."
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
          </InputGroup>

          <div className="flex flex-1 flex-wrap gap-3">
            <Select items={PRIORITY_ITEMS} value={priority} onValueChange={setPriority}>
              <SelectTrigger className="w-[140px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="">All Priorities</SelectItem>
                {PRIORITIES.map((p) => (
                  <SelectItem key={p} value={p}>
                    {p}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select items={CATEGORY_ITEMS} value={category} onValueChange={setCategory}>
              <SelectTrigger className="w-[150px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="">All Categories</SelectItem>
                {CATEGORIES.map((c) => (
                  <SelectItem key={c} value={c}>
                    {c}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select items={DATE_RANGE_ITEMS} value={dateRange} onValueChange={(v) => {
              setDateRange(v)
              if (v !== "custom") {
                setCustomFrom("")
                setCustomTo("")
              }
            }}>
              <SelectTrigger className="w-[140px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="">All Time</SelectItem>
                {DATE_RANGES.map((dr) => (
                  <SelectItem key={dr.value} value={dr.value}>
                    {dr.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            {dateRange === "custom" && (
              <>
                <input
                  type="date"
                  value={customFrom}
                  onChange={(e) => setCustomFrom(e.target.value)}
                  className="flex h-9 rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                />
                <input
                  type="date"
                  value={customTo}
                  onChange={(e) => setCustomTo(e.target.value)}
                  className="flex h-9 rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                />
              </>
            )}

            {activeFilters > 0 && (
              <Button variant="ghost" onClick={clearFilters}>
                <XIcon data-icon="inline-start" />
                Clear ({activeFilters})
              </Button>
            )}
          </div>
        </div>

        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Showing <span className="font-medium text-foreground">{filtered.length}</span>{" "}
            of {tickets.length} tickets
          </p>
        </div>

        {ticketsLoading ? (
          <div className="flex flex-col gap-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        ) : (
          <TicketsTable tickets={filtered} />
        )}
      </Card>
    </div>
  )
}
