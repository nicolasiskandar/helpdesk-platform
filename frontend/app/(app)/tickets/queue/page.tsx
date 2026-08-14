"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import { useStore } from "@/lib/store"
import { RoleGuard } from "@/components/role-guard"
import type { Ticket } from "@/lib/types"
import { Button } from "@/components/ui/button"
import { Card } from "@/components/ui/card"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Skeleton } from "@/components/ui/skeleton"
import { HandIcon, ArrowRightIcon } from "lucide-react"
import { toast } from "sonner"
import { formatRelative } from "@/lib/analytics"
import { PriorityIndicator } from "@/components/ticket-badges"
import {
  Empty,
  EmptyHeader,
  EmptyTitle,
  EmptyDescription,
} from "@/components/ui/empty"

export default function TicketQueuePage() {
  const {
    openUnassignedTickets,
    openUnassignedTicketsLoading,
    fetchOpenUnassignedTickets,
    claimTicket,
  } = useStore()
  const router = useRouter()
  const [claimingId, setClaimingId] = React.useState<string | null>(null)

  React.useEffect(() => {
    fetchOpenUnassignedTickets()
  }, [fetchOpenUnassignedTickets])

  async function handleClaim(ticket: Ticket) {
    setClaimingId(ticket.id)
    try {
      await claimTicket(ticket.id)
      toast.success("Ticket claimed", {
        description: `${ticket.reference} is now assigned to you.`,
      })
      router.push(`/tickets/${ticket.id}`)
    } catch {
      toast.error("Failed to claim ticket")
    } finally {
      setClaimingId(null)
    }
  }

  return (
    <RoleGuard allowedRoles={["admin", "agent", "manager"]}>
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold tracking-tight text-balance">
          Ticket Queue
        </h1>
        <p className="text-sm text-muted-foreground">
          Pick up open tickets to start working on them.
        </p>
      </div>

      <Card className="flex flex-col gap-4 p-4">
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Showing{" "}
            <span className="font-medium text-foreground">
              {openUnassignedTickets.length}
            </span>{" "}
            available tickets
          </p>
        </div>

        {openUnassignedTicketsLoading ? (
          <div className="flex flex-col gap-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        ) : openUnassignedTickets.length === 0 ? (
          <Empty className="py-10">
            <EmptyHeader>
              <EmptyTitle>No open tickets</EmptyTitle>
              <EmptyDescription>
                There are no unassigned tickets available for pickup right now.
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : (
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className="w-24">Ref</TableHead>
                  <TableHead className="min-w-[220px]">Subject</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Priority</TableHead>
                  <TableHead>Created</TableHead>
                  <TableHead className="text-right">Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {openUnassignedTickets.map((ticket) => (
                  <TableRow key={ticket.id} className="group">
                    <TableCell className="font-mono text-xs text-muted-foreground">
                      {ticket.reference}
                    </TableCell>
                    <TableCell>
                      <span className="font-medium line-clamp-1">
                        {ticket.subject}
                      </span>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {ticket.category}
                    </TableCell>
                    <TableCell>
                      <PriorityIndicator priority={ticket.priority} />
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {formatRelative(ticket.createdAt)}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-2">
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={claimingId === ticket.id}
                          onClick={() => handleClaim(ticket)}
                        >
                          <HandIcon data-icon="inline-start" className="size-4" />
                          {claimingId === ticket.id ? "Claiming..." : "Pick Up"}
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          render={
                            <a href={`/tickets/${ticket.id}`}>
                              <ArrowRightIcon className="size-4" />
                            </a>
                          }
                        />
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </Card>
    </div>
    </RoleGuard>
  )
}
