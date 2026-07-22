"use client"

import Link from "next/link"
import {
  Inbox,
  Loader,
  Clock,
  CheckCircle2,
  AlertTriangle,
  UserPlus,
  ArrowRight,
  Activity,
} from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  CardAction,
} from "@/components/ui/card"
import { Progress } from "@/components/ui/progress"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { StatCard } from "@/components/stat-card"
import { TicketsTable } from "@/components/tickets-table"
import { CategoryChart } from "@/components/dashboard/category-chart"
import { PriorityChart } from "@/components/dashboard/priority-chart"
import { useStore } from "@/lib/store"
import { useAuth } from "@/lib/auth"
import { ROLE_LABELS } from "@/lib/data"
import {
  ticketStats,
  byCategory,
  byPriority,
  agentPerformance,
  formatRelative,
} from "@/lib/analytics"

function initials(name: string) {
  return name
    .split(" ")
    .map((n) => n[0])
    .slice(0, 2)
    .join("")
}

export default function DashboardPage() {
  const { tickets, role, currentUserId, ticketsLoading } = useStore()
  const { user: authUser } = useAuth()

  const displayName = authUser?.fullName || "User"
  const firstName = displayName.split(" ")[0]

  // Employees only see their own tickets.
  const scoped =
    role === "employee"
      ? tickets.filter((t) => t.requesterId === currentUserId)
      : tickets

  const stats = ticketStats(scoped)
  const categories = byCategory(scoped)
  const priorities = byPriority(scoped).filter((p) => p.count > 0)
  const performance = agentPerformance(tickets)

  const recent = [...scoped]
    .sort((a, b) => +new Date(b.updatedAt) - +new Date(a.updatedAt))
    .slice(0, 6)

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h2 className="text-xl font-semibold tracking-tight text-balance">
          Welcome back, {firstName}
        </h2>
        <p className="text-sm text-muted-foreground">
          {role === "employee"
            ? "Here's the status of your support requests."
            : `Operational overview for the service desk — viewing as ${ROLE_LABELS[role]}.`}
        </p>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard
          label="Open Tickets"
          value={stats.open}
          icon={Inbox}
          accent="info"
          hint="Current open"
        />
        <StatCard
          label="In Progress"
          value={stats.inProgress}
          icon={Loader}
          accent="primary"
          hint="Being worked on"
        />
        <StatCard
          label="Pending"
          value={stats.pending}
          icon={Clock}
          accent="warning"
          hint="Awaiting response"
        />
        <StatCard
          label="Resolved"
          value={stats.resolved}
          icon={CheckCircle2}
          accent="success"
          hint="Completed"
        />
      </div>

      {role !== "employee" && (stats.critical > 0 || stats.unassigned > 0) ? (
        <div className="grid gap-4 sm:grid-cols-2">
          {stats.critical > 0 && (
            <Card className="border-destructive/30 bg-destructive/5">
              <CardContent className="flex items-center gap-3 p-4">
                <div className="flex size-9 items-center justify-center rounded-lg bg-destructive/10 text-destructive">
                  <AlertTriangle className="size-5" />
                </div>
                <div className="flex-1">
                  <p className="text-sm font-medium">
                    {stats.critical} critical ticket
                    {stats.critical === 1 ? "" : "s"} need attention
                  </p>
                  <p className="text-xs text-muted-foreground">
                    Prioritize to stay within SLA
                  </p>
                </div>
                <Button
                  size="sm"
                  variant="outline"
                  render={<Link href="/tickets">Review</Link>}
                />
              </CardContent>
            </Card>
          )}
          {stats.unassigned > 0 && (
            <Card>
              <CardContent className="flex items-center gap-3 p-4">
                <div className="flex size-9 items-center justify-center rounded-lg bg-muted text-muted-foreground">
                  <UserPlus className="size-5" />
                </div>
                <div className="flex-1">
                  <p className="text-sm font-medium">
                    {stats.unassigned} unassigned ticket
                    {stats.unassigned === 1 ? "" : "s"}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    Assign to an available agent
                  </p>
                </div>
                <Button
                  size="sm"
                  variant="outline"
                  render={<Link href="/tickets">Assign</Link>}
                />
              </CardContent>
            </Card>
          )}
        </div>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <CategoryChart data={categories} />
        </div>
        <PriorityChart data={priorities} />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Recent Tickets</CardTitle>
            <CardDescription>Latest activity across the queue</CardDescription>
            <CardAction>
              <Button
                variant="ghost"
                size="sm"
                render={
                  <Link href="/tickets">
                    View all
                    <ArrowRight data-icon="inline-end" />
                  </Link>
                }
              />
            </CardAction>
          </CardHeader>
          <CardContent className="px-0">
            {ticketsLoading ? (
              <div className="flex items-center justify-center py-8 text-sm text-muted-foreground">
                Loading tickets...
              </div>
            ) : (
              <TicketsTable tickets={recent} compact />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Agent Performance</CardTitle>
            <CardDescription>Resolved vs active workload</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            {performance.length === 0 ? (
              <p className="py-4 text-center text-sm text-muted-foreground">
                No agent assignments yet.
              </p>
            ) : (
              performance.map((agent) => {
                const rate =
                  agent.assigned > 0
                    ? Math.round((agent.resolved / agent.assigned) * 100)
                    : 0
                return (
                  <div key={agent.id} className="flex flex-col gap-1.5">
                    <div className="flex items-center justify-between text-sm">
                      <div className="flex items-center gap-2">
                        <Avatar className="size-6">
                          <AvatarFallback className="bg-muted text-[10px]">
                            {agent.name.slice(0, 2).toUpperCase()}
                          </AvatarFallback>
                        </Avatar>
                        <span className="font-medium">{agent.name}</span>
                      </div>
                      <span className="text-xs text-muted-foreground tabular-nums">
                        {agent.resolved}/{agent.assigned} resolved
                      </span>
                    </div>
                    <Progress value={rate} className="h-1.5" />
                  </div>
                )
              })
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
