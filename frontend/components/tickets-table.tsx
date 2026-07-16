"use client"

import Link from "next/link"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import {
  Empty,
  EmptyHeader,
  EmptyDescription,
  EmptyTitle,
} from "@/components/ui/empty"
import { StatusBadge, PriorityIndicator } from "@/components/ticket-badges"
import { getUser } from "@/lib/data"
import { formatRelative } from "@/lib/analytics"
import type { Ticket } from "@/lib/types"

function initials(name: string) {
  return name
    .split(" ")
    .map((n) => n[0])
    .slice(0, 2)
    .join("")
}

export function TicketsTable({
  tickets,
  compact = false,
}: {
  tickets: Ticket[]
  compact?: boolean
}) {
  if (tickets.length === 0) {
    return (
      <Empty className="py-10">
        <EmptyHeader>
          <EmptyTitle>No tickets found</EmptyTitle>
          <EmptyDescription>
            Try adjusting your filters or create a new ticket.
          </EmptyDescription>
        </EmptyHeader>
      </Empty>
    )
  }

  return (
    <div className="overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow className="hover:bg-transparent">
            <TableHead className="w-24">Ref</TableHead>
            <TableHead className="min-w-[220px]">Subject</TableHead>
            {!compact && <TableHead>Category</TableHead>}
            <TableHead>Priority</TableHead>
            <TableHead>Status</TableHead>
            {!compact && <TableHead>Assignee</TableHead>}
            <TableHead className="text-right">Updated</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {tickets.map((ticket) => {
            const assignee = getUser(ticket.assigneeId)
            return (
              <TableRow key={ticket.id} className="group">
                <TableCell className="font-mono text-xs text-muted-foreground">
                  {ticket.reference}
                </TableCell>
                <TableCell>
                  <Link
                    href={`/tickets/${ticket.reference}`}
                    className="font-medium underline-offset-4 hover:underline"
                  >
                    <span className="line-clamp-1">{ticket.subject}</span>
                  </Link>
                </TableCell>
                {!compact && (
                  <TableCell className="text-sm text-muted-foreground">
                    {ticket.category}
                  </TableCell>
                )}
                <TableCell>
                  <PriorityIndicator priority={ticket.priority} />
                </TableCell>
                <TableCell>
                  <StatusBadge status={ticket.status} />
                </TableCell>
                {!compact && (
                  <TableCell>
                    {assignee ? (
                      <div className="flex items-center gap-2">
                        <Avatar className="size-6">
                          <AvatarFallback className="bg-muted text-[10px]">
                            {initials(assignee.name)}
                          </AvatarFallback>
                        </Avatar>
                        <span className="text-sm">{assignee.name}</span>
                      </div>
                    ) : (
                      <span className="text-sm text-muted-foreground">
                        Unassigned
                      </span>
                    )}
                  </TableCell>
                )}
                <TableCell className="text-right text-sm text-muted-foreground">
                  {formatRelative(ticket.updatedAt)}
                </TableCell>
              </TableRow>
            )
          })}
        </TableBody>
      </Table>
    </div>
  )
}
