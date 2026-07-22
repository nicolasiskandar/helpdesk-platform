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
import { formatRelative } from "@/lib/analytics"
import type { Ticket } from "@/lib/types"

function initials(id: string) {
  return id.slice(0, 2).toUpperCase()
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
          {tickets.map((ticket) => (
            <TableRow key={ticket.id} className="group">
              <TableCell className="font-mono text-xs text-muted-foreground">
                {ticket.reference}
              </TableCell>
              <TableCell>
                <Link
                  href={`/tickets/${ticket.id}`}
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
                  {ticket.assigneeId ? (
                    <div className="flex items-center gap-2">
                      <Avatar className="size-6">
                        <AvatarFallback className="bg-muted text-[10px]">
                          {initials(ticket.assigneeId)}
                        </AvatarFallback>
                      </Avatar>
                      <span className="text-sm">
                        {ticket.assigneeId.slice(0, 8)}
                      </span>
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
          ))}
        </TableBody>
      </Table>
    </div>
  )
}
