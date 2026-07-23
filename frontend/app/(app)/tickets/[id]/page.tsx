"use client"

import * as React from "react"
import { useParams, useRouter } from "next/navigation"
import { toast } from "sonner"
import {
  ArrowLeftIcon,
  SendIcon,
  PaperclipIcon,
  UserPlusIcon,
  UserMinusIcon,
  ClockIcon,
  PencilIcon,
  TrashIcon,
} from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Label } from "@/components/ui/label"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Separator } from "@/components/ui/separator"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Empty,
  EmptyHeader,
  EmptyTitle,
  EmptyDescription,
} from "@/components/ui/empty"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { StatusBadge, PriorityIndicator } from "@/components/ticket-badges"
import { useStore } from "@/lib/store"
import { formatRelative, formatDateTime } from "@/lib/analytics"
import type { Ticket, Comment, TicketStatus, TicketCategory, TicketPriority } from "@/lib/types"
import type { AuditLogEntryResponse, AttachmentResponse, CategoryResponse, PriorityResponse } from "@/lib/api"
import { apiGetTicketByReference, apiGetCategories, apiGetPriorities } from "@/lib/api"

function initials(id: string) {
  return id.slice(0, 2).toUpperCase()
}

const STATUSES: { value: TicketStatus; label: string }[] = [
  { value: "Open", label: "Open" },
  { value: "In Progress", label: "In Progress" },
  { value: "Pending", label: "Pending" },
  { value: "Resolved", label: "Resolved" },
  { value: "Closed", label: "Closed" },
]

const STATUS_IDS: Record<TicketStatus, number> = {
  Open: 1,
  "In Progress": 2,
  Pending: 3,
  Resolved: 4,
  Closed: 5,
}

const CATEGORY_IDS: Record<TicketCategory, number> = {
  Hardware: 1,
  Software: 2,
  Network: 3,
  Access: 4,
  Other: 5,
}

const PRIORITY_IDS: Record<TicketPriority, number> = {
  Low: 1,
  Medium: 2,
  High: 3,
  Critical: 4,
}

export default function TicketDetailPage() {
  const params = useParams()
  const router = useRouter()
  const {
    loadTicketDetail,
    loadComments,
    loadAuditLog,
    loadAttachments,
    addComment,
    assignTicket,
    updateTicket,
    deleteTicket,
    currentUserId,
    role,
  } = useStore()

  const paramId = params.id as string
  const [ticket, setTicket] = React.useState<Ticket | null>(null)
  const [comments, setComments] = React.useState<Comment[]>([])
  const [auditLog, setAuditLog] = React.useState<AuditLogEntryResponse[]>([])
  const [attachments, setAttachments] = React.useState<AttachmentResponse[]>([])
  const [loading, setLoading] = React.useState(true)
  const [commentText, setCommentText] = React.useState("")
  const [isInternal, setIsInternal] = React.useState(false)
  const [submittingComment, setSubmittingComment] = React.useState(false)

  const [editOpen, setEditOpen] = React.useState(false)
  const [editTitle, setEditTitle] = React.useState("")
  const [editDescription, setEditDescription] = React.useState("")
  const [editCategory, setEditCategory] = React.useState<TicketCategory>("Other")
  const [editPriority, setEditPriority] = React.useState<TicketPriority>("Medium")
  const [savingEdit, setSavingEdit] = React.useState(false)
  const [categories, setCategories] = React.useState<CategoryResponse[]>([])
  const [priorities, setPriorities] = React.useState<PriorityResponse[]>([])

  const [deleteOpen, setDeleteOpen] = React.useState(false)
  const [deleting, setDeleting] = React.useState(false)

  React.useEffect(() => {
    let cancelled = false
    async function load() {
      setLoading(true)
      // Try loading by ID first; if it fails and param looks like a reference, try by reference
      let t = await loadTicketDetail(paramId)
      if (!t && !paramId.match(/^[0-9a-f-]{36}$/i)) {
        try {
          const res = await apiGetTicketByReference(paramId)
          t = await loadTicketDetail(res.id)
        } catch { /* ignore */ }
      }
      if (cancelled) return
      if (t) {
        setTicket(t)
        // Load related data using the ticket ID
        try {
          const c = await loadComments(t.id)
          if (!cancelled) setComments(c)
        } catch { /* ignore */ }
        try {
          const a = await loadAuditLog(t.id)
          if (!cancelled) setAuditLog(a)
        } catch { /* ignore */ }
        try {
          const att = await loadAttachments(t.id)
          if (!cancelled) setAttachments(att)
        } catch { /* ignore */ }
      }
      setLoading(false)
    }
    load()
    apiGetCategories().then(setCategories).catch(() => {})
    apiGetPriorities().then(setPriorities).catch(() => {})
    return () => { cancelled = true }
  }, [paramId, loadTicketDetail, loadComments, loadAuditLog, loadAttachments])

  async function handleAddComment() {
    if (!ticket || !commentText.trim() || submittingComment) return
    setSubmittingComment(true)
    try {
      await addComment(ticket.id, commentText.trim(), isInternal)
      setCommentText("")
      setIsInternal(false)
      // Reload comments
      const c = await loadComments(ticket.id)
      setComments(c)
      toast.success("Comment added")
    } catch {
      toast.error("Failed to add comment")
    } finally {
      setSubmittingComment(false)
    }
  }

  async function handleStatusChange(newStatus: TicketStatus) {
    if (!ticket) return
    try {
      await updateTicket(ticket.id, { status: newStatus })
      setTicket({ ...ticket, status: newStatus })
      toast.success(`Status changed to ${newStatus}`)
    } catch {
      toast.error("Failed to change status")
    }
  }

  function openEditDialog() {
    if (!ticket) return
    setEditTitle(ticket.subject)
    setEditDescription(ticket.description)
    setEditCategory(ticket.category)
    setEditPriority(ticket.priority)
    setEditOpen(true)
  }

  async function handleSaveEdit() {
    if (!ticket) return
    setSavingEdit(true)
    try {
      await updateTicket(ticket.id, {
        subject: editTitle,
        description: editDescription,
        category: editCategory,
        priority: editPriority,
      })
      setTicket({
        ...ticket,
        subject: editTitle,
        description: editDescription,
        category: editCategory,
        priority: editPriority,
      })
      setEditOpen(false)
      toast.success("Ticket updated")
    } catch {
      toast.error("Failed to update ticket")
    } finally {
      setSavingEdit(false)
    }
  }

  async function handleDelete() {
    if (!ticket) return
    setDeleting(true)
    try {
      await deleteTicket(ticket.id)
      toast.success("Ticket deleted")
      router.push("/tickets")
    } catch {
      toast.error("Failed to delete ticket")
      setDeleting(false)
    }
  }

  if (loading) {
    return (
      <div className="flex flex-col gap-6">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (!ticket) {
    return (
      <div className="flex flex-col gap-6">
        <Button variant="ghost" onClick={() => router.back()}>
          <ArrowLeftIcon data-icon="inline-start" /> Back
        </Button>
        <Empty className="py-10">
          <EmptyHeader>
            <EmptyTitle>Ticket not found</EmptyTitle>
            <EmptyDescription>
              The ticket you&apos;re looking for doesn&apos;t exist or has been removed.
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      </div>
    )
  }

  const canChangeStatus = role === "admin" || role === "agent" || role === "manager"
  const isOpen = ticket.status === "Open"
  const isCreator = ticket.requesterId === currentUserId
  const isAdmin = role === "admin"
  const canEdit = isOpen && (isCreator || isAdmin)
  const canDelete = isOpen && (isCreator || isAdmin)

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon-sm" onClick={() => router.back()}>
          <ArrowLeftIcon />
        </Button>
        <div className="flex flex-1 flex-col gap-1">
          <div className="flex items-center gap-2">
            <span className="font-mono text-sm text-muted-foreground">
              {ticket.reference}
            </span>
            <StatusBadge status={ticket.status} />
          </div>
          <h1 className="text-xl font-semibold tracking-tight text-balance">
            {ticket.subject}
          </h1>
        </div>
        <div className="flex items-center gap-2">
          {canEdit && (
            <Button variant="outline" size="sm" onClick={openEditDialog}>
              <PencilIcon data-icon="inline-start" /> Edit
            </Button>
          )}
          {canDelete && (
            <Button variant="destructive" size="sm" onClick={() => setDeleteOpen(true)}>
              <TrashIcon data-icon="inline-start" /> Delete
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="flex flex-col gap-6 lg:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle>Description</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="whitespace-pre-wrap text-sm text-muted-foreground">
                {ticket.description}
              </p>
            </CardContent>
          </Card>

          <Tabs defaultValue="comments">
            <TabsList>
              <TabsTrigger value="comments">
                Comments ({comments.length})
              </TabsTrigger>
              <TabsTrigger value="activity">
                Activity ({auditLog.length})
              </TabsTrigger>
              <TabsTrigger value="attachments">
                Attachments ({attachments.length})
              </TabsTrigger>
            </TabsList>

            <TabsContent value="comments" className="flex flex-col gap-4">
              <Card>
                <CardContent className="flex flex-col gap-3 pt-4">
                  <Textarea
                    placeholder="Add a comment..."
                    value={commentText}
                    onChange={(e) => setCommentText(e.target.value)}
                    rows={3}
                  />
                  <div className="flex items-center justify-between">
                    {role !== "employee" && (
                      <label className="flex items-center gap-2 text-sm text-muted-foreground">
                        <input
                          type="checkbox"
                          checked={isInternal}
                          onChange={(e) => setIsInternal(e.target.checked)}
                          className="accent-primary"
                        />
                        Internal note
                      </label>
                    )}
                    <Button
                      size="sm"
                      onClick={handleAddComment}
                      disabled={!commentText.trim() || submittingComment}
                    >
                      {submittingComment ? "Posting..." : "Post Comment"}
                      {!submittingComment && <SendIcon data-icon="inline-end" />}
                    </Button>
                  </div>
                </CardContent>
              </Card>

              {comments.length === 0 ? (
                <p className="py-4 text-center text-sm text-muted-foreground">
                  No comments yet.
                </p>
              ) : (
                comments.map((c) => (
                  <Card key={c.id}>
                    <CardContent className="flex gap-3 p-4">
                      <Avatar className="size-8">
                        <AvatarFallback className="bg-muted text-[10px]">
                          {initials(c.authorId)}
                        </AvatarFallback>
                      </Avatar>
                      <div className="flex-1">
                        <div className="flex items-center gap-2">
                          <span className="text-sm font-medium">
                            {c.authorId === currentUserId ? "You" : c.authorId.slice(0, 8)}
                          </span>
                          {c.internal && (
                            <Badge variant="outline" className="text-[10px]">
                              Internal
                            </Badge>
                          )}
                          <span className="text-xs text-muted-foreground">
                            {formatRelative(c.createdAt)}
                          </span>
                        </div>
                        <p className="mt-1 whitespace-pre-wrap text-sm">
                          {c.body}
                        </p>
                      </div>
                    </CardContent>
                  </Card>
                ))
              )}
            </TabsContent>

            <TabsContent value="activity">
              <Card>
                <CardContent className="flex flex-col gap-0 p-0">
                  {auditLog.length === 0 ? (
                    <p className="py-4 text-center text-sm text-muted-foreground">
                      No activity recorded.
                    </p>
                  ) : (
                    auditLog.map((entry, i) => (
                      <div key={entry.id}>
                        <div className="flex gap-3 px-4 py-3">
                          <ClockIcon className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                          <div className="flex-1">
                            <p className="text-sm">
                              <span className="font-medium">{entry.fieldChanged}</span>
                              {entry.oldValue && entry.newValue ? (
                                <>
                                  {" changed from "}
                                  <span className="text-muted-foreground">{entry.oldValue}</span>
                                  {" to "}
                                  <span className="text-muted-foreground">{entry.newValue}</span>
                                </>
                              ) : entry.newValue ? (
                                <> set to <span className="text-muted-foreground">{entry.newValue}</span></>
                              ) : null}
                            </p>
                            <p className="text-xs text-muted-foreground">
                              {entry.changedByType} · {formatDateTime(entry.changedAt)}
                            </p>
                          </div>
                        </div>
                        {i < auditLog.length - 1 && <Separator />}
                      </div>
                    ))
                  )}
                </CardContent>
              </Card>
            </TabsContent>

            <TabsContent value="attachments">
              <Card>
                <CardContent className="p-4">
                  {attachments.length === 0 ? (
                    <p className="py-4 text-center text-sm text-muted-foreground">
                      No attachments.
                    </p>
                  ) : (
                    <div className="flex flex-col gap-2">
                      {attachments.map((a) => (
                        <div
                          key={a.id}
                          className="flex items-center gap-3 rounded-md border p-3"
                        >
                          <PaperclipIcon className="size-4 text-muted-foreground" />
                          <div className="flex-1">
                            <p className="text-sm font-medium">{a.fileName}</p>
                            <p className="text-xs text-muted-foreground">
                              {formatDateTime(a.uploadedAt)}
                            </p>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </CardContent>
              </Card>
            </TabsContent>
          </Tabs>
        </div>

        <div className="flex flex-col gap-4">
          <Card>
            <CardHeader>
              <CardTitle>Details</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-4 text-sm">
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Status</span>
                {canChangeStatus ? (
                  <Select
                    value={ticket.status}
                    onValueChange={(v) => handleStatusChange(v as TicketStatus)}
                  >
                    <SelectTrigger className="w-[140px] h-8 text-xs">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {STATUSES.map((s) => (
                        <SelectItem key={s.value} value={s.value}>
                          {s.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <StatusBadge status={ticket.status} />
                )}
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Priority</span>
                <PriorityIndicator priority={ticket.priority} />
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Category</span>
                <span>{ticket.category}</span>
              </div>
              <Separator />
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Created</span>
                <span>{formatDateTime(ticket.createdAt)}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Updated</span>
                <span>{formatRelative(ticket.updatedAt)}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">SLA</span>
                <span>{ticket.slaHours}h</span>
              </div>
            </CardContent>
          </Card>

          {(role === "admin" || role === "agent" || role === "manager") && (
            <Card>
              <CardHeader>
                <CardTitle>Assignment</CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col gap-3">
                {ticket.assigneeId ? (
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <Avatar className="size-7">
                        <AvatarFallback className="bg-muted text-[10px]">
                          {initials(ticket.assigneeId)}
                        </AvatarFallback>
                      </Avatar>
                      <span className="text-sm">{ticket.assigneeId.slice(0, 8)}</span>
                    </div>
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      onClick={async () => {
                        await assignTicket(ticket.id, null)
                        setTicket({ ...ticket, assigneeId: null })
                        toast.success("Agent unassigned")
                      }}
                    >
                      <UserMinusIcon className="size-4" />
                    </Button>
                  </div>
                ) : (
                  <p className="text-sm text-muted-foreground">Unassigned</p>
                )}
              </CardContent>
            </Card>
          )}
        </div>
      </div>

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Edit Ticket</DialogTitle>
            <DialogDescription>
              Make changes to the ticket. Only open tickets can be edited.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-4 py-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="edit-title">Title</Label>
              <Input
                id="edit-title"
                value={editTitle}
                onChange={(e) => setEditTitle(e.target.value)}
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={editDescription}
                onChange={(e) => setEditDescription(e.target.value)}
                rows={4}
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="flex flex-col gap-2">
                <Label>Category</Label>
                <Select value={editCategory} onValueChange={(v) => setEditCategory(v as TicketCategory)}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {categories.map((c) => (
                      <SelectItem key={c.id} value={c.name}>
                        {c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="flex flex-col gap-2">
                <Label>Priority</Label>
                <Select value={editPriority} onValueChange={(v) => setEditPriority(v as TicketPriority)}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {priorities.map((p) => (
                      <SelectItem key={p.id} value={p.name}>
                        {p.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={handleSaveEdit}
              disabled={savingEdit || !editTitle.trim() || !editDescription.trim()}
            >
              {savingEdit ? "Saving..." : "Save Changes"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Delete Ticket</DialogTitle>
            <DialogDescription>
              Are you sure you want to delete <span className="font-medium text-foreground">{ticket.reference}</span>?
              This action cannot be undone. All comments, attachments, and activity will be permanently removed.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={handleDelete}
              disabled={deleting}
            >
              {deleting ? "Deleting..." : "Delete Ticket"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
