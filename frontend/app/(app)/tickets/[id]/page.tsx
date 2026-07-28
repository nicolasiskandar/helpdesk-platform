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
  HandIcon,
  XIcon,
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
import type { AuditLogEntryResponse, AttachmentResponse, CategoryResponse, PriorityResponse, UserResponse } from "@/lib/api"
import { apiGetTicketByReference, apiGetCategories, apiGetPriorities, apiAttachmentDownloadUrl, apiGetUsers, apiUnassignAgent, apiUploadAttachment } from "@/lib/api"

function initials(name: string) {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0])
    .join("")
    .toUpperCase()
    .slice(0, 2)
}

const STATUS_IDS: Record<TicketStatus, number> = {
  Open: 1,
  "In Progress": 2,
  "Pending Resolution": 3,
  Closed: 4,
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
    claimTicket,
    updateTicket,
    deleteTicket,
    currentUserId,
    role,
    userMap,
  } = useStore()

  const paramId = params.id as string
  const [ticket, setTicket] = React.useState<Ticket | null>(null)
  const [comments, setComments] = React.useState<Comment[]>([])
  const [auditLog, setAuditLog] = React.useState<AuditLogEntryResponse[]>([])
  const [attachments, setAttachments] = React.useState<AttachmentResponse[]>([])
  const [loading, setLoading] = React.useState(true)
  const [commentText, setCommentText] = React.useState("")
  const [isPrivate, setIsPrivate] = React.useState(false)
  const [submittingComment, setSubmittingComment] = React.useState(false)

  const [editOpen, setEditOpen] = React.useState(false)
  const [editTitle, setEditTitle] = React.useState("")
  const [editDescription, setEditDescription] = React.useState("")
  const [editCategory, setEditCategory] = React.useState<TicketCategory>("Other")
  const [editPriority, setEditPriority] = React.useState<TicketPriority>("Medium")
  const [savingEdit, setSavingEdit] = React.useState(false)
  const [categories, setCategories] = React.useState<CategoryResponse[]>([])
  const [priorities, setPriorities] = React.useState<PriorityResponse[]>([])
  const [editFiles, setEditFiles] = React.useState<File[]>([])
  const editFileInputRef = React.useRef<HTMLInputElement>(null)

  const MAX_FILE_SIZE = 10 * 1024 * 1024

  function handleEditFilesSelected(e: React.ChangeEvent<HTMLInputElement>) {
    const selected = Array.from(e.target.files || [])
    const valid = selected.filter((f) => {
      if (f.size > MAX_FILE_SIZE) { toast.warning(`${f.name} exceeds 10 MB limit`); return false }
      return true
    })
    setEditFiles((prev) => [...prev, ...valid])
    if (editFileInputRef.current) editFileInputRef.current.value = ""
  }

  function removeEditFile(index: number) {
    setEditFiles((prev) => prev.filter((_, i) => i !== index))
  }

  const [deleteOpen, setDeleteOpen] = React.useState(false)
  const [deleting, setDeleting] = React.useState(false)
  const [claiming, setClaiming] = React.useState(false)
  const [assigning, setAssigning] = React.useState(false)
  const [unassigningId, setUnassigningId] = React.useState<string | null>(null)
  const [agents, setAgents] = React.useState<UserResponse[]>([])

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
    apiGetUsers(undefined, undefined, true, 1, 200).then((res) => {
      if (!cancelled) setAgents(res.users.filter((u) => u.role === "IT Support Agent"))
    }).catch(() => {})
    return () => { cancelled = true }
  }, [paramId, loadTicketDetail, loadComments, loadAuditLog, loadAttachments])

  async function handleAddComment() {
    if (!ticket || !commentText.trim() || submittingComment) return
    setSubmittingComment(true)
    try {
      await addComment(ticket.id, commentText.trim(), isPrivate)
      setCommentText("")
      setIsPrivate(false)
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

  async function handleConfirmResolution() {
    if (!ticket) return
    try {
      await updateTicket(ticket.id, { status: "Closed" })
      setTicket({ ...ticket, status: "Closed" })
      toast.success("Resolution confirmed — ticket closed")
    } catch {
      toast.error("Failed to confirm resolution")
    }
  }

  async function handleReopenTicket() {
    if (!ticket) return
    try {
      await updateTicket(ticket.id, { status: "In Progress" })
      setTicket({ ...ticket, status: "In Progress" })
      toast.success("Ticket reopened")
    } catch {
      toast.error("Failed to reopen ticket")
    }
  }

  async function handleResolve() {
    if (!ticket) return
    try {
      await updateTicket(ticket.id, { status: "Pending Resolution" })
      setTicket({ ...ticket, status: "Pending Resolution" })
      toast.success("Ticket marked as resolved — awaiting confirmation")
    } catch {
      toast.error("Failed to resolve ticket")
    }
  }

  function openEditDialog() {
    if (!ticket) return
    setEditTitle(ticket.subject)
    setEditDescription(ticket.description)
    setEditCategory(ticket.category)
    setEditPriority(ticket.priority)
    setEditFiles([])
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
      for (const file of editFiles) {
        try {
          await apiUploadAttachment(ticket.id, file)
        } catch (err: any) {
          toast.error(`Failed to upload ${file.name}`, { description: err?.message || "Upload failed" })
        }
      }
      const updatedAttachments = await loadAttachments(ticket.id)
      setAttachments(updatedAttachments)
      setTicket({
        ...ticket,
        subject: editTitle,
        description: editDescription,
        category: editCategory,
        priority: editPriority,
      })
      setEditFiles([])
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

  async function handleClaim() {
    if (!ticket) return
    setClaiming(true)
    try {
      setTicket({
        ...ticket,
        assigneeId: currentUserId,
        assigneeIds: [currentUserId],
        assigneeName: userMap[currentUserId] || ticket.assigneeName,
        status: ticket.status === "Open" ? "In Progress" : ticket.status,
      })
      await claimTicket(ticket.id)
      toast.success("Ticket claimed", {
        description: `${ticket.reference} is now assigned to you.`,
      })
    } catch {
      toast.error("Failed to claim ticket")
      const refreshed = await loadTicketDetail(ticket.id)
      if (refreshed) setTicket(refreshed)
    } finally {
      setClaiming(false)
    }
  }

  async function handleAssign(agentId: string) {
    if (!ticket || !agentId || assigning) return
    setAssigning(true)
    try {
      await assignTicket(ticket.id, agentId)
      const assigneeIds = Array.from(new Set([...ticket.assigneeIds, agentId]))
      setTicket({
        ...ticket,
        assigneeId: assigneeIds[0] ?? null,
        assigneeIds,
        assigneeName: userMap[agentId] || ticket.assigneeName,
        status: ticket.status === "Open" ? "In Progress" : ticket.status,
      })
      toast.success("Agent assigned")
    } catch {
      toast.error("Failed to assign agent")
    } finally {
      setAssigning(false)
    }
  }

  async function handleUnassign(agentId: string) {
    if (!ticket || unassigningId) return
    setUnassigningId(agentId)
    try {
      await apiUnassignAgent(ticket.id, agentId)
      const assigneeIds = ticket.assigneeIds.filter((id) => id !== agentId)
      setTicket({
        ...ticket,
        assigneeId: assigneeIds[0] ?? null,
        assigneeIds,
        status: assigneeIds.length === 0 && ticket.status === "In Progress" ? "Open" : ticket.status,
      })
      toast.success("Agent unassigned")
    } catch {
      toast.error("Failed to unassign agent")
    } finally {
      setUnassigningId(null)
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
  const canManageAssignments = role === "admin" || role === "manager"
  const activeAssigneeIds = ticket.assigneeIds.length > 0
    ? ticket.assigneeIds
    : ticket.assigneeId
      ? [ticket.assigneeId]
      : []
  const assignableAgents = agents.filter((agent) => !activeAssigneeIds.includes(agent.id))
  const isAssignedAgent = activeAssigneeIds.includes(currentUserId)
  const canSeePrivate = isCreator || isAssignedAgent || isAdmin

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
                    {canSeePrivate && (
                      <label className="flex items-center gap-2 text-sm text-muted-foreground">
                        <input
                          type="checkbox"
                          checked={isPrivate}
                          onChange={(e) => setIsPrivate(e.target.checked)}
                          className="accent-primary"
                        />
                        Private
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
                          {(userMap[c.authorId] || c.authorId).slice(0, 2).toUpperCase()}
                        </AvatarFallback>
                      </Avatar>
                      <div className="flex-1">
                        <div className="flex items-center gap-2">
                          <span className="text-sm font-medium">
                            {c.authorId === currentUserId ? "You" : (userMap[c.authorId] || c.authorId.slice(0, 8))}
                          </span>
                          {c.isPrivate && (
                            <Badge variant="outline" className="text-[10px]">
                              Private
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
                        <button
                          key={a.id}
                          type="button"
                          onClick={async () => {
                            const res = await fetch(apiAttachmentDownloadUrl(ticket.id, a.id), {
                              headers: { Authorization: `Bearer ${sessionStorage.getItem("accessToken") || ""}` },
                            })
                            if (!res.ok) return
                            const blob = await res.blob()
                            const url = URL.createObjectURL(blob)
                            const link = document.createElement("a")
                            link.href = url
                            link.download = a.fileName
                            link.click()
                            URL.revokeObjectURL(url)
                          }}
                          className="flex items-center gap-3 rounded-md border p-3 text-left transition-colors hover:bg-muted/50"
                        >
                          <PaperclipIcon className="size-4 text-muted-foreground shrink-0" />
                          <div className="flex-1 min-w-0">
                            <p className="text-sm font-medium truncate">{a.fileName}</p>
                            <p className="text-xs text-muted-foreground">
                              {formatDateTime(a.uploadedAt)}
                            </p>
                          </div>
                        </button>
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
                <StatusBadge status={ticket.status} />
              </div>

              {/* Agent / Manager / Admin action buttons */}
              {canChangeStatus && ticket.status === "Open" && (
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => handleStatusChange("In Progress")}
                  className="w-full"
                >
                  Start Working
                </Button>
              )}
              {canChangeStatus && ticket.status === "In Progress" && (
                <Button
                  size="sm"
                  variant="default"
                  onClick={handleResolve}
                  className="w-full"
                >
                  Resolve Ticket
                </Button>
              )}
              {canChangeStatus && ticket.status === "Pending Resolution" && role === "admin" && (
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    variant="default"
                    onClick={handleConfirmResolution}
                    className="flex-1"
                  >
                    Close
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={handleReopenTicket}
                    className="flex-1"
                  >
                    Reopen
                  </Button>
                </div>
              )}

              {/* Employee (ticket creator) action buttons */}
              {!canChangeStatus && ticket.status === "Pending Resolution" && isCreator && (
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    variant="default"
                    onClick={handleConfirmResolution}
                    className="flex-1"
                  >
                    Confirm Resolved
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={handleReopenTicket}
                    className="flex-1"
                  >
                    Reopen
                  </Button>
                </div>
              )}
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
                {activeAssigneeIds.length > 0 ? (
                  <div className="flex flex-col gap-2">
                    {activeAssigneeIds.map((agentId) => (
                      <div key={agentId} className="flex items-center justify-between rounded-md border px-3 py-2">
                        <div className="flex min-w-0 items-center gap-2">
                          <Avatar className="size-7">
                            <AvatarFallback className="bg-muted text-[10px]">
                              {(userMap[agentId] || agentId).slice(0, 2).toUpperCase()}
                            </AvatarFallback>
                          </Avatar>
                          <span className="truncate text-sm">{userMap[agentId] || agentId.slice(0, 8)}</span>
                        </div>
                        {canManageAssignments && (
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            onClick={() => handleUnassign(agentId)}
                            disabled={unassigningId === agentId}
                          >
                            <UserMinusIcon className="size-4" />
                          </Button>
                        )}
                      </div>
                    ))}
                    {canManageAssignments && assignableAgents.length > 0 && (
                      <Select
                        value=""
                        onValueChange={handleAssign}
                        disabled={assigning}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder={assigning ? "Assigning..." : "Add another agent..."} />
                        </SelectTrigger>
                        <SelectContent>
                          {assignableAgents.map((a) => (
                            <SelectItem key={a.id} value={a.id}>
                              {a.fullName}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  </div>
                ) : canManageAssignments && agents.length > 0 ? (
                  <Select
                    value=""
                    onValueChange={handleAssign}
                    disabled={assigning}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={assigning ? "Assigning..." : "Assign to agent..."} />
                    </SelectTrigger>
                    <SelectContent>
                      {agents.map((a) => (
                        <SelectItem key={a.id} value={a.id}>
                          {a.fullName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <div className="flex items-center gap-2 rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
                    <UserPlusIcon className="size-4" />
                    Unassigned
                  </div>
                )}
              </CardContent>
            </Card>
          )}

          {ticket.status === "Open" && activeAssigneeIds.length === 0 && role === "agent" && (
            <Card>
              <CardHeader>
                <CardTitle>Pick Up Ticket</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground mb-3">
                  Claim this ticket to start working on it. It will be assigned to you and moved to In Progress.
                </p>
                <Button
                  onClick={handleClaim}
                  disabled={claiming}
                  className="w-full"
                >
                  <HandIcon data-icon="inline-start" className="size-4" />
                  {claiming ? "Claiming..." : "Pick Up This Ticket"}
                </Button>
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
            <div className="flex flex-col gap-2">
              <Label>Attachments</Label>
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => editFileInputRef.current?.click()}
                >
                  <PaperclipIcon />
                  Add files
                </Button>
                <span className="text-xs text-muted-foreground">Max 10 MB per file</span>
              </div>
              <input
                ref={editFileInputRef}
                type="file"
                multiple
                className="hidden"
                onChange={handleEditFilesSelected}
              />
              {editFiles.length > 0 && (
                <div className="flex flex-col gap-1.5 mt-1">
                  {editFiles.map((file, i) => (
                    <div key={`${file.name}-${i}`} className="flex items-center justify-between rounded-md border px-3 py-1.5 text-sm">
                      <span className="truncate mr-2">{file.name}</span>
                      <div className="flex items-center gap-2 shrink-0">
                        <span className="text-muted-foreground text-xs">
                          {file.size < 1024 ? `${file.size} B` : file.size < 1024 * 1024 ? `${(file.size / 1024).toFixed(1)} KB` : `${(file.size / (1024 * 1024)).toFixed(1)} MB`}
                        </span>
                        <button type="button" onClick={() => removeEditFile(i)} className="text-muted-foreground hover:text-foreground">
                          <XIcon className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
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
