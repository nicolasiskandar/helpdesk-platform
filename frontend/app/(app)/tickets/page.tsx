"use client"

import * as React from "react"
import { useStore, statusBadgeClass } from "@/lib/store"
import { users } from "@/lib/data"
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
  "Email",
  "Security",
  "Other",
]

export default function TicketsPage() {
  const { tickets, role, currentUserId } = useStore()
  const [query, setQuery] = React.useState("")
  const [status, setStatus] = React.useState<string>("all")
  const [priority, setPriority] = React.useState<string>("all")
  const [category, setCategory] = React.useState<string>("all")
  const [assignee, setAssignee] = React.useState<string>("all")

  const agents = users.filter(
    (u) => u.role === "agent" || u.role === "admin"
  )

  const filtered = React.useMemo(() => {
    return tickets.filter((t) => {
      // Employees only see their own tickets.
      if (role === "employee" && t.requesterId !== currentUserId) return false
      if (status !== "all" && t.status !== status) return false
      if (priority !== "all" && t.priority !== priority) return false
      if (category !== "all" && t.category !== category) return false
      if (assignee !== "all") {
        if (assignee === "unassigned" && t.assigneeId) return false
        if (assignee === "me" && t.assigneeId !== currentUserId) return false
        if (
          assignee !== "unassigned" &&
          assignee !== "me" &&
          t.assigneeId !== assignee
        )
          return false
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
  }, [tickets, role, currentUserId, status, priority, category, assignee, query])

  const activeFilters =
    (status !== "all" ? 1 : 0) +
    (priority !== "all" ? 1 : 0) +
    (category !== "all" ? 1 : 0) +
    (assignee !== "all" ? 1 : 0)

  function clearFilters() {
    setStatus("all")
    setPriority("all")
    setCategory("all")
    setAssignee("all")
    setQuery("")
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
        <Button asChild>
          <Link href="/tickets/new">
            <PlusIcon data-icon="inline-start" />
            New Ticket
          </Link>
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
            <Select value={priority} onValueChange={setPriority}>
              <SelectTrigger className="w-[140px]">
                <SelectValue placeholder="Priority" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Priorities</SelectItem>
                {PRIORITIES.map((p) => (
                  <SelectItem key={p} value={p}>
                    {p}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select value={category} onValueChange={setCategory}>
              <SelectTrigger className="w-[150px]">
                <SelectValue placeholder="Category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Categories</SelectItem>
                {CATEGORIES.map((c) => (
                  <SelectItem key={c} value={c}>
                    {c}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            {role !== "employee" && (
              <Select value={assignee} onValueChange={setAssignee}>
                <SelectTrigger className="w-[160px]">
                  <SelectValue placeholder="Assignee" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Assignees</SelectItem>
                  <SelectItem value="me">Assigned to me</SelectItem>
                  <SelectItem value="unassigned">Unassigned</SelectItem>
                  {agents.map((a) => (
                    <SelectItem key={a.id} value={a.id}>
                      {a.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
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

        <TicketsTable tickets={filtered} showRequester={role !== "employee"} />
      </Card>
    </div>
  )
}
