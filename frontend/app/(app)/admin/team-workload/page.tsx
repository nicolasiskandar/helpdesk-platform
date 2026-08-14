"use client"

import Link from "next/link"
import * as React from "react"
import { toast } from "sonner"
import {
  ArrowRightIcon,
  CheckCircle2Icon,
  CircleDotIcon,
  RefreshCwIcon,
  SearchIcon,
  UserPlusIcon,
  UsersIcon,
} from "lucide-react"

import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import {
  Empty,
  EmptyDescription,
  EmptyHeader,
  EmptyTitle,
} from "@/components/ui/empty"
import { Input } from "@/components/ui/input"
import { Progress } from "@/components/ui/progress"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { RoleGuard } from "@/components/role-guard"
import { PriorityIndicator, StatusBadge } from "@/components/ticket-badges"
import { formatRelative } from "@/lib/analytics"
import {
  apiAssignAgent,
  apiGetAgentWorkload,
  apiGetOpenUnassignedTickets,
  apiGetUsers,
} from "@/lib/api"
import type {
  AgentWorkloadResponse,
  AgentWorkloadTicketResponse,
  TicketResponse,
} from "@/lib/api"
import type { TicketPriority, TicketStatus } from "@/lib/types"

type AgentRow = AgentWorkloadResponse & {
  fullName: string
  email: string
}

const EMPTY_WORKLOAD: Omit<AgentWorkloadResponse, "agentUserId"> = {
  openCount: 0,
  resolvedCount: 0,
  openTickets: [],
  resolvedTickets: [],
}

const KNOWN_STATUSES: TicketStatus[] = [
  "Open",
  "In Progress",
  "Resolved - Pending Confirmation",
  "Closed",
  "Resolved by AI",
]

function initials(name: string) {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase() || "IT"
}

function statusName(name: string): TicketStatus {
  return (KNOWN_STATUSES as string[]).includes(name)
    ? (name as TicketStatus)
    : "Open"
}

function ticketPriority(name: string): TicketPriority {
  return (["Low", "Medium", "High", "Critical"].includes(name) ? name : "Medium") as TicketPriority
}

function ticketAge(ticket: AgentWorkloadTicketResponse) {
  return ticket.statusName === "Open" || ticket.statusName === "In Progress"
    ? formatRelative(ticket.createdAt)
    : formatRelative(ticket.updatedAt)
}

export default function TeamWorkloadPage() {
  const [agents, setAgents] = React.useState<AgentRow[]>([])
  const [unassignedTickets, setUnassignedTickets] = React.useState<TicketResponse[]>([])
  const [selectedAgentId, setSelectedAgentId] = React.useState<string>("")
  const [query, setQuery] = React.useState("")
  const [loading, setLoading] = React.useState(true)
  const [assigningTicketId, setAssigningTicketId] = React.useState<string | null>(null)
  const [targetAgents, setTargetAgents] = React.useState<Record<string, string>>({})

  const load = React.useCallback(async () => {
    setLoading(true)
    try {
      const [workload, usersRes, queue] = await Promise.all([
        apiGetAgentWorkload(),
        apiGetUsers(undefined, 2, true, 1, 200),
        apiGetOpenUnassignedTickets(1, 200),
      ])

      const workloadMap = new Map(workload.map((item) => [item.agentUserId, item]))
      const rows = usersRes.users
        .map((user) => {
          const item = workloadMap.get(user.id) ?? { agentUserId: user.id, ...EMPTY_WORKLOAD }
          return {
            ...item,
            fullName: user.fullName,
            email: user.email,
          }
        })
        .sort((a, b) => b.openCount - a.openCount || b.resolvedCount - a.resolvedCount || a.fullName.localeCompare(b.fullName))

      setAgents(rows)
      setUnassignedTickets(queue.tickets)
      setSelectedAgentId((current) => current || rows[0]?.agentUserId || "")
    } catch {
      toast.error("Failed to load team workload")
      setAgents([])
      setUnassignedTickets([])
    } finally {
      setLoading(false)
    }
  }, [])

  React.useEffect(() => {
    load()
  }, [load])

  const filteredAgents = React.useMemo(() => {
    const needle = query.trim().toLowerCase()
    if (!needle) return agents
    return agents.filter((agent) =>
      `${agent.fullName} ${agent.email}`.toLowerCase().includes(needle)
    )
  }, [agents, query])

  const selectedAgent = agents.find((agent) => agent.agentUserId === selectedAgentId) ?? agents[0]
  const totalOpen = agents.reduce((sum, agent) => sum + agent.openCount, 0)
  const totalResolved = agents.reduce((sum, agent) => sum + agent.resolvedCount, 0)
  const busiestOpen = Math.max(...agents.map((agent) => agent.openCount), 1)
  const availableAgents = [...agents].sort((a, b) => a.openCount - b.openCount || a.fullName.localeCompare(b.fullName))
  const agentItems = React.useMemo(() => {
    const items: Record<string, string> = {}
    for (const agent of availableAgents) {
      items[agent.agentUserId] = `${agent.fullName} (${agent.openCount} open)`
    }
    return items
  }, [availableAgents])

  async function assignQueuedTicket(ticketId: string) {
    const agentId = targetAgents[ticketId]
    if (!agentId) {
      toast.error("Choose an agent first")
      return
    }

    setAssigningTicketId(ticketId)
    try {
      await apiAssignAgent(ticketId, agentId)
      toast.success("Ticket assigned")
      await load()
      setTargetAgents((current) => {
        const next = { ...current }
        delete next[ticketId]
        return next
      })
    } catch {
      toast.error("Failed to assign ticket")
    } finally {
      setAssigningTicketId(null)
    }
  }

  return (
    <RoleGuard allowedRoles={["admin", "manager"]}>
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold tracking-tight">Team Workload</h1>
          <p className="text-sm text-muted-foreground">
            Assign open tickets and review each IT agent&apos;s active and solved work.
          </p>
        </div>
        <Button variant="outline" onClick={load} disabled={loading}>
          <RefreshCwIcon data-icon="inline-start" className={loading ? "animate-spin" : ""} />
          Refresh
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <MetricCard title="Active workload" value={totalOpen} icon={<CircleDotIcon />} />
        <MetricCard title="Solved tickets" value={totalResolved} icon={<CheckCircle2Icon />} />
        <MetricCard title="Available agents" value={agents.length} icon={<UsersIcon />} />
      </div>

      <div className="grid gap-6 xl:grid-cols-[360px_1fr]">
        <Card className="overflow-hidden">
          <CardHeader className="gap-3">
            <CardTitle>Agents</CardTitle>
            <div className="relative">
              <SearchIcon className="absolute left-2.5 top-2.5 size-4 text-muted-foreground" />
              <Input
                className="pl-8"
                placeholder="Search agents"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
              />
            </div>
          </CardHeader>
          <CardContent className="flex max-h-[560px] flex-col gap-2 overflow-y-auto">
            {loading ? (
              Array.from({ length: 5 }).map((_, index) => (
                <Skeleton key={index} className="h-20 w-full" />
              ))
            ) : filteredAgents.length === 0 ? (
              <p className="py-8 text-center text-sm text-muted-foreground">No agents found.</p>
            ) : (
              filteredAgents.map((agent) => (
                <button
                  key={agent.agentUserId}
                  type="button"
                  onClick={() => setSelectedAgentId(agent.agentUserId)}
                  className={`rounded-md border p-3 text-left transition-colors hover:bg-muted/50 ${
                    selectedAgent?.agentUserId === agent.agentUserId ? "border-primary bg-primary/5" : ""
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <Avatar className="size-9">
                      <AvatarFallback>{initials(agent.fullName)}</AvatarFallback>
                    </Avatar>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">{agent.fullName}</p>
                      <p className="truncate text-xs text-muted-foreground">{agent.email}</p>
                    </div>
                    <Badge variant={agent.openCount > 0 ? "default" : "secondary"}>
                      {agent.openCount} open
                    </Badge>
                  </div>
                  <div className="mt-3 flex items-center gap-2">
                    <Progress value={(agent.openCount / busiestOpen) * 100} className="h-1.5" />
                    <span className="text-xs tabular-nums text-muted-foreground">
                      {agent.resolvedCount} solved
                    </span>
                  </div>
                </button>
              ))
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
              <div className="flex items-center gap-3">
                <Avatar className="size-10">
                  <AvatarFallback>{selectedAgent ? initials(selectedAgent.fullName) : "IT"}</AvatarFallback>
                </Avatar>
                <div>
                  <CardTitle>{selectedAgent?.fullName ?? "No agent selected"}</CardTitle>
                  <p className="text-sm text-muted-foreground">{selectedAgent?.email}</p>
                </div>
              </div>
              {selectedAgent ? (
                <div className="flex gap-2">
                  <Badge>{selectedAgent.openCount} active</Badge>
                  <Badge variant="secondary">{selectedAgent.resolvedCount} solved</Badge>
                </div>
              ) : null}
            </div>
          </CardHeader>
          <CardContent>
            {loading ? (
              <Skeleton className="h-80 w-full" />
            ) : selectedAgent ? (
              <Tabs defaultValue="open">
                <TabsList>
                  <TabsTrigger value="open">Current ({selectedAgent.openCount})</TabsTrigger>
                  <TabsTrigger value="resolved">Solved ({selectedAgent.resolvedCount})</TabsTrigger>
                </TabsList>
                <TabsContent value="open">
                  <TicketTable tickets={selectedAgent.openTickets} emptyText="No active tickets for this agent." />
                </TabsContent>
                <TabsContent value="resolved">
                  <TicketTable tickets={selectedAgent.resolvedTickets} emptyText="No solved tickets recorded for this agent." />
                </TabsContent>
              </Tabs>
            ) : (
              <Empty className="py-12">
                <EmptyHeader>
                  <EmptyTitle>No IT agents</EmptyTitle>
                  <EmptyDescription>Create an active IT Support Agent user to start assigning work.</EmptyDescription>
                </EmptyHeader>
              </Empty>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <div className="flex flex-col gap-1">
            <CardTitle>Unassigned Tickets</CardTitle>
            <p className="text-sm text-muted-foreground">
              Route open tickets to the agent with the best current capacity.
            </p>
          </div>
        </CardHeader>
        <CardContent>
          {loading ? (
            <Skeleton className="h-40 w-full" />
          ) : unassignedTickets.length === 0 ? (
            <Empty className="py-10">
              <EmptyHeader>
                <EmptyTitle>No tickets waiting for assignment</EmptyTitle>
                <EmptyDescription>Every open ticket already has an active assignee.</EmptyDescription>
              </EmptyHeader>
            </Empty>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Ticket</TableHead>
                    <TableHead>Priority</TableHead>
                    <TableHead>Created</TableHead>
                    <TableHead className="min-w-[220px]">Assign To</TableHead>
                    <TableHead className="text-right">Action</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {unassignedTickets.map((ticket) => (
                    <TableRow key={ticket.id}>
                      <TableCell>
                        <div className="flex flex-col gap-1">
                          <Link href={`/tickets/${ticket.id}`} className="font-medium hover:underline">
                            {ticket.title}
                          </Link>
                          <span className="font-mono text-xs text-muted-foreground">{ticket.referenceNumber}</span>
                        </div>
                      </TableCell>
                      <TableCell>
                        <PriorityIndicator priority={ticketPriority(ticket.priorityName)} />
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {formatRelative(ticket.createdAt)}
                      </TableCell>
                      <TableCell>
                        <Select
                          items={agentItems}
                          value={targetAgents[ticket.id] ?? ""}
                          onValueChange={(agentId) =>
                            setTargetAgents((current) => ({ ...current, [ticket.id]: agentId ?? "" }))
                          }
                        >
                          <SelectTrigger className="w-full">
                            <SelectValue placeholder="Choose agent" />
                          </SelectTrigger>
                          <SelectContent>
                            {availableAgents.map((agent) => (
                              <SelectItem key={agent.agentUserId} value={agent.agentUserId}>
                                {agent.fullName} ({agent.openCount} open)
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </TableCell>
                      <TableCell className="text-right">
                        <Button
                          size="sm"
                          onClick={() => assignQueuedTicket(ticket.id)}
                          disabled={assigningTicketId === ticket.id || agents.length === 0}
                        >
                          <UserPlusIcon data-icon="inline-start" />
                          {assigningTicketId === ticket.id ? "Assigning..." : "Assign"}
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
    </RoleGuard>
  )
}

function MetricCard({
  title,
  value,
  icon,
}: {
  title: string
  value: number
  icon: React.ReactNode
}) {
  return (
    <Card>
      <CardContent className="flex items-center justify-between p-4">
        <div>
          <p className="text-sm text-muted-foreground">{title}</p>
          <p className="text-2xl font-semibold tabular-nums">{value}</p>
        </div>
        <div className="flex size-10 items-center justify-center rounded-md bg-muted text-muted-foreground">
          {icon}
        </div>
      </CardContent>
    </Card>
  )
}

function TicketTable({
  tickets,
  emptyText,
}: {
  tickets: AgentWorkloadTicketResponse[]
  emptyText: string
}) {
  if (tickets.length === 0) {
    return <p className="py-10 text-center text-sm text-muted-foreground">{emptyText}</p>
  }

  return (
    <div className="overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Ticket</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Priority</TableHead>
            <TableHead>Age</TableHead>
            <TableHead className="text-right">Open</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {tickets.map((ticket) => (
            <TableRow key={ticket.ticketId}>
              <TableCell>
                <div className="flex flex-col gap-1">
                  <Link href={`/tickets/${ticket.ticketId}`} className="font-medium hover:underline">
                    {ticket.title}
                  </Link>
                  <span className="font-mono text-xs text-muted-foreground">
                    {ticket.referenceNumber} · {ticket.categoryName}
                  </span>
                </div>
              </TableCell>
              <TableCell>
                <StatusBadge status={statusName(ticket.statusName)} />
              </TableCell>
              <TableCell>
                <PriorityIndicator priority={ticketPriority(ticket.priorityName)} />
              </TableCell>
              <TableCell className="text-sm text-muted-foreground">
                {ticketAge(ticket)}
              </TableCell>
              <TableCell className="text-right">
                <Button
                  variant="ghost"
                  size="icon-sm"
                  render={
                    <Link href={`/tickets/${ticket.ticketId}`}>
                      <ArrowRightIcon />
                    </Link>
                  }
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}
