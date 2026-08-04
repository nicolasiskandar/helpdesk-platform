"use client"

import * as React from "react"
import { useParams, useRouter, useSearchParams } from "next/navigation"
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
  ReplyIcon,
  ChevronDownIcon,
} from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Label } from "@/components/ui/label"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Separator } from "@/components/ui/separator"
import { Checkbox } from "@/components/ui/checkbox"
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover"
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
import { AttachmentUpload } from "@/components/attachment-upload"
import { AttachmentPreview, isImageFile } from "@/components/attachment-preview"
import { useStore } from "@/lib/store"
import { formatRelative, formatDateTime, formatDuration, formatFileSize } from "@/lib/analytics"
import type { Ticket, Comment, TicketStatus, TicketCategory, TicketPriority } from "@/lib/types"
import type { AuditLogEntryResponse, AttachmentResponse, CategoryResponse, PriorityResponse, UserResponse } from "@/lib/api"
import { apiGetTicketByReference, apiGetCategories, apiGetPriorities, apiAttachmentDownloadUrl, apiGetUsers, apiUnassignAgent, apiUploadAttachment, apiEscalateTicket, apiDeleteAttachment } from "@/lib/api"

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
  Resolved: 5,
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
  const searchParams = useSearchParams()
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
  const [accessDenied, setAccessDenied] = React.useState(false)
  const [commentText, setCommentText] = React.useState("")
  const [submittingComment, setSubmittingComment] = React.useState(false)
  const [replyTo, setReplyTo] = React.useState<string | null>(null)
  const [recipientIds, setRecipientIds] = React.useState<string[]>([])
  const [highlightCommentId, setHighlightCommentId] = React.useState<string | null>(null)
  const scrolledKeyRef = React.useRef<string | null>(null)
  const [replyCandidateIds, setReplyCandidateIds] = React.useState<string[]>([])
  const [recipientOpen, setRecipientOpen] = React.useState(false)

  const [editOpen, setEditOpen] = React.useState(false)
  const [editTitle, setEditTitle] = React.useState("")
  const [editDescription, setEditDescription] = React.useState("")
  const [editCategory, setEditCategory] = React.useState<TicketCategory>("Other")
  const [editPriority, setEditPriority] = React.useState<TicketPriority>("Medium")
  const [savingEdit, setSavingEdit] = React.useState(false)
  const [categories, setCategories] = React.useState<CategoryResponse[]>([])
  const [priorities, setPriorities] = React.useState<PriorityResponse[]>([])
  const [editFiles, setEditFiles] = React.useState<File[]>([])

  const [uploadFiles, setUploadFiles] = React.useState<File[]>([])
  const [uploading, setUploading] = React.useState(false)

  async function handleUploadAttachments() {
    if (!ticket || uploadFiles.length === 0 || uploading) return
    setUploading(true)
    try {
      for (const file of uploadFiles) {
        await apiUploadAttachment(ticket.id, file)
      }
      const att = await loadAttachments(ticket.id)
      setAttachments(att)
      toast.success(
        `Uploaded ${uploadFiles.length} file${uploadFiles.length === 1 ? "" : "s"}`
      )
      setUploadFiles([])
    } catch (err: any) {
      toast.error(err?.message || "Failed to upload attachments.")
    } finally {
      setUploading(false)
    }
  }

  async function handleDeleteAttachment(attachmentId: string) {
    if (!ticket) return
    try {
      await apiDeleteAttachment(ticket.id, attachmentId)
      setAttachments((prev) => prev.filter((a) => a.id !== attachmentId))
      toast.success("Attachment deleted")
    } catch (err: any) {
      toast.error(err?.message || "Failed to delete attachment.")
    }
  }

  const canDeleteAttachment = (a: AttachmentResponse) =>
    role === "admin" ||
    (ticket?.requesterId != null && ticket.requesterId === currentUserId) ||
    a.uploadedByUserId === currentUserId

  const [deleteOpen, setDeleteOpen] = React.useState(false)
  const [deleting, setDeleting] = React.useState(false)
  const [claiming, setClaiming] = React.useState(false)
  const [assigning, setAssigning] = React.useState(false)
  const [unassigningId, setUnassigningId] = React.useState<string | null>(null)
  const [escalateOpen, setEscalateOpen] = React.useState(false)
  const [escalateReason, setEscalateReason] = React.useState("")
  const [escalating, setEscalating] = React.useState(false)
  const [agents, setAgents] = React.useState<UserResponse[]>([])
  const [users, setUsers] = React.useState<UserResponse[]>([])

  React.useEffect(() => {
    let cancelled = false
    async function load() {
      setLoading(true)
      setAccessDenied(false)
      try {
        // Try loading by ID first; if it fails and param looks like a reference, try by reference
        let t = await loadTicketDetail(paramId)
        if (!t && !paramId.match(/^[0-9a-f-]{36}$/i)) {
          try {
            const res = await apiGetTicketByReference(paramId)
            t = await loadTicketDetail(res.id)
          } catch (err: any) {
            if (err?.status === 403) throw err
          }
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
      } catch (err: any) {
        if (!cancelled && err?.status === 403) setAccessDenied(true)
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    load()
    apiGetCategories().then(setCategories).catch(() => {})
    apiGetPriorities().then(setPriorities).catch(() => {})
    apiGetUsers(undefined, undefined, true, 1, 500).then((res) => {
      if (!cancelled) {
        setAgents(res.users.filter((u) => u.role === "IT Support Agent"))
        setUsers(res.users)
      }
    }).catch(() => {})
    return () => { cancelled = true }
  }, [paramId, loadTicketDetail, loadComments, loadAuditLog, loadAttachments])

  const commentParam = searchParams.get("comment")
  React.useEffect(() => {
    if (!commentParam) return
    const key = `${ticket?.id ?? paramId}:${commentParam}`
    if (scrolledKeyRef.current === key) return
    const target = document.getElementById(`comment-${commentParam}`)
    if (target) {
      scrolledKeyRef.current = key
      setHighlightCommentId(commentParam)
      target.scrollIntoView({ behavior: "smooth", block: "center" })
      const t = setTimeout(() => setHighlightCommentId(null), 2500)
      return () => clearTimeout(t)
    }
    // Comments are loaded but the target isn't visible (private/restricted) — don't retry
    if (ticket && !loading) scrolledKeyRef.current = key
  }, [commentParam, comments, ticket, loading, paramId])

  async function handleAddComment() {
    if (!ticket || !commentText.trim() || submittingComment) return
    setSubmittingComment(true)
    try {
      const parent = replyTo ? comments.find((c) => c.id === replyTo) : undefined
      const nothingSelected = recipientIds.length === 0
      // Replying to a private comment that had no recipients: omit the field so the backend
      // inherits the private audience (creator + assigned agents) instead of sending [].
      const omitRecipients =
        replyTo && replyCandidateIds.length === 0 && nothingSelected && parent?.isPrivate
      await addComment(
        ticket.id,
        commentText.trim(),
        replyTo ?? undefined,
        omitRecipients ? undefined : recipientIds
      )
      setCommentText("")
      setReplyTo(null)
      setRecipientIds([])
      setReplyCandidateIds([])
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
      try {
        const refreshed = await loadTicketDetail(ticket.id)
        if (refreshed) setTicket(refreshed)
      } catch { /* ignore */ }
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

  async function handleEscalate() {
    if (!ticket || escalating) return
    setEscalating(true)
    try {
      await apiEscalateTicket(ticket.id, escalateReason.trim() || undefined)
      const assigneeIds = ticket.assigneeIds.filter((id) => id !== currentUserId)
      setTicket({
        ...ticket,
        assigneeId: assigneeIds[0] ?? null,
        assigneeIds,
        status: "Open",
      })
      setEscalateReason("")
      setEscalateOpen(false)
      toast.success("Ticket returned to the queue", {
        description: `${ticket.reference} is open for another agent to pick up.`,
      })
    } catch {
      toast.error("Failed to escalate ticket")
    } finally {
      setEscalating(false)
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

  if (accessDenied) {
    return (
      <div className="flex flex-col gap-6">
        <Button variant="ghost" onClick={() => router.back()}>
          <ArrowLeftIcon data-icon="inline-start" /> Back
        </Button>
        <Empty className="py-10">
          <EmptyHeader>
            <EmptyTitle>Access denied</EmptyTitle>
            <EmptyDescription>
              You don&apos;t have access to this ticket. It&apos;s only visible to its creator,
              assigned agents, managers, and admins until it is closed.
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
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

  // Comment recipients may only be agents, managers, or the ticket creator
  // (admins are excluded — they can see everything regardless)
  const selectableUsers = users.filter(
    (u) =>
      u.id !== currentUserId &&
      u.isActive &&
      (u.role === "IT Support Agent" || u.role === "Manager" || u.id === ticket.requesterId)
  )
  const isReplying = replyTo != null
  // Reply recipients are limited to the parent comment's recipients — the reply can keep
  // all or fewer of them, but can never reference anyone new.
  const replyCandidateUsers = replyCandidateIds
    .filter((id) => id !== currentUserId)
    .map((id) => users.find((u) => u.id === id))
    .filter((u): u is NonNullable<typeof u> => u != null)
  const replyCandidateUnknown = replyCandidateIds.filter(
    (id) => id !== currentUserId && !users.some((u) => u.id === id)
  )

  const commentName = (comment: Comment) =>
    comment.authorId === currentUserId
      ? "You"
      : (userMap[comment.authorId] || comment.authorId.slice(0, 8))

  const renderComment = (comment: Comment) => {
    const replies = comments.filter((c) => c.parentId === comment.id)
    const isReplyTarget = replyTo === comment.id
    return (
      <Card
        key={comment.id}
        id={`comment-${comment.id}`}
        className={`scroll-mt-24 ${comment.parentId ? "border-l-2 border-l-primary/20" : ""} ${
          highlightCommentId === comment.id ? "ring-2 ring-primary" : ""
        }`}
      >
        <CardContent className="flex gap-3 p-4">
          <Avatar className="size-8">
            <AvatarFallback className="bg-muted text-[10px]">
              {(userMap[comment.authorId] || comment.authorId).slice(0, 2).toUpperCase()}
            </AvatarFallback>
          </Avatar>
          <div className="flex-1">
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium">
                {commentName(comment)}
              </span>
              {comment.recipientIds.length > 0 && (
                <Badge variant="secondary" className="text-[10px]">
                  Targeted
                </Badge>
              )}
              <span className="text-xs text-muted-foreground">
                {formatRelative(comment.createdAt)}
              </span>
            </div>
            <p className="mt-1 whitespace-pre-wrap text-sm">
              {comment.body}
            </p>
            <button
              type="button"
              className="mt-2 inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
              onClick={() => {
                setReplyTo(isReplyTarget ? null : comment.id)
                setReplyCandidateIds(isReplyTarget ? [] : [...comment.recipientIds])
                setRecipientIds(isReplyTarget ? [] : [...comment.recipientIds])
              }}
            >
              <ReplyIcon className="size-3" />
              {isReplyTarget ? "Cancel reply" : "Reply"}
            </button>
          </div>
        </CardContent>
        {replies.length > 0 && (
          <div className="flex flex-col gap-3 px-4 pb-4">
            {replies.map(renderComment)}
          </div>
        )}
      </Card>
    )
  }

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
                  {replyTo && (
                    <div className="flex items-center justify-between rounded-md bg-muted px-3 py-2 text-xs text-muted-foreground">
                      <span className="flex items-center gap-1.5">
                        <ReplyIcon className="size-3" />
                        Replying to {commentName(comments.find((c) => c.id === replyTo)!)}
                      </span>
                      <button
                        type="button"
                        className="text-muted-foreground hover:text-foreground"
                        onClick={() => { setReplyTo(null); setRecipientIds([]); setReplyCandidateIds([]) }}
                      >
                        <XIcon className="size-3.5" />
                      </button>
                    </div>
                  )}
                  <Textarea
                    placeholder={replyTo ? "Write a reply..." : "Add a comment..."}
                    value={commentText}
                    onChange={(e) => setCommentText(e.target.value)}
                    rows={3}
                  />
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex items-center gap-3">
                      <Popover open={recipientOpen} onOpenChange={setRecipientOpen}>
                        <PopoverTrigger render={<Button type="button" variant="outline" size="sm" />}>
                          <span className="flex items-center gap-1.5 text-xs">
                            {recipientIds.length > 0 ? `Select Recipient (${recipientIds.length})` : "Select Recipient"}
                            <ChevronDownIcon className="size-3" />
                          </span>
                        </PopoverTrigger>
                        <PopoverContent className="w-64 p-2">
                          <div className="max-h-56 overflow-y-auto">
                            {isReplying ? (
                              replyCandidateUsers.length === 0 && replyCandidateUnknown.length === 0 ? (
                                <p className="px-2 py-1 text-xs text-muted-foreground">
                                  This comment had no recipients.
                                </p>
                              ) : (
                                <>
                                  {replyCandidateUsers.map((u) => {
                                    const checked = recipientIds.includes(u.id)
                                    return (
                                      <label
                                        key={u.id}
                                        className="flex cursor-pointer items-center gap-2 rounded px-2 py-1.5 text-sm hover:bg-muted"
                                      >
                                        <Checkbox
                                          checked={checked}
                                          onCheckedChange={() =>
                                            setRecipientIds((prev) =>
                                              checked ? prev.filter((id) => id !== u.id) : [...prev, u.id]
                                            )
                                          }
                                        />
                                        <span className="flex-1 truncate">{u.fullName}</span>
                                        <span className="text-[10px] text-muted-foreground">
                                          {u.role === "IT Support Agent" ? "Agent" : u.role}
                                        </span>
                                      </label>
                                    )
                                  })}
                                  {replyCandidateUnknown.map((id) => {
                                    const checked = recipientIds.includes(id)
                                    return (
                                      <label
                                        key={id}
                                        className="flex cursor-pointer items-center gap-2 rounded px-2 py-1.5 text-sm hover:bg-muted"
                                      >
                                        <Checkbox
                                          checked={checked}
                                          onCheckedChange={() =>
                                            setRecipientIds((prev) =>
                                              checked ? prev.filter((x) => x !== id) : [...prev, id]
                                            )
                                          }
                                        />
                                        <span className="flex-1 truncate">{userMap[id] || id.slice(0, 8)}</span>
                                      </label>
                                    )
                                  })}
                                </>
                              )
                            ) : selectableUsers.length === 0 ? (
                              <p className="px-2 py-1 text-xs text-muted-foreground">
                                No eligible recipients.
                              </p>
                            ) : (
                              selectableUsers.map((u) => {
                                const checked = recipientIds.includes(u.id)
                                return (
                                  <label
                                    key={u.id}
                                    className="flex cursor-pointer items-center gap-2 rounded px-2 py-1.5 text-sm hover:bg-muted"
                                  >
                                    <Checkbox
                                      checked={checked}
                                      onCheckedChange={() =>
                                        setRecipientIds((prev) =>
                                          checked ? prev.filter((id) => id !== u.id) : [...prev, u.id]
                                        )
                                      }
                                    />
                                    <span className="flex-1 truncate">{u.fullName}</span>
                                    <span className="text-[10px] text-muted-foreground">
                                      {u.role === "IT Support Agent" ? "Agent" : u.role}
                                    </span>
                                  </label>
                                )
                              })
                            )}
                          </div>
                        </PopoverContent>
                      </Popover>
                    </div>
                    <Button
                      size="sm"
                      onClick={handleAddComment}
                      disabled={!commentText.trim() || submittingComment}
                    >
                      {submittingComment ? "Posting..." : replyTo ? "Post Reply" : "Post Comment"}
                      {!submittingComment && <SendIcon data-icon="inline-end" />}
                    </Button>
                  </div>
                  {isReplying && replyCandidateIds.length > 0 && (
                    <p className="text-xs text-muted-foreground">
                      Reply recipients are limited to the people selected on the parent comment.
                    </p>
                  )}
                  {recipientIds.length > 0 && (
                    <p className="text-xs text-muted-foreground">
                      Targeted comments are visible only to the selected recipient(s).
                    </p>
                  )}
                </CardContent>
              </Card>

              {comments.length === 0 ? (
                <p className="py-4 text-center text-sm text-muted-foreground">
                  No comments yet.
                </p>
              ) : (
                <div className="flex flex-col gap-3">
                  {comments.filter((c) => !c.parentId).map(renderComment)}
                </div>
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
                <CardContent className="flex flex-col gap-4 p-4">
                  <AttachmentUpload
                    files={uploadFiles}
                    onChange={setUploadFiles}
                    label={uploadFiles.length > 0 ? undefined : "Attachments"}
                  />
                  {uploadFiles.length > 0 && (
                    <div className="flex justify-end">
                      <Button
                        size="sm"
                        onClick={handleUploadAttachments}
                        disabled={uploading}
                      >
                        {uploading ? "Uploading..." : "Upload Files"}
                      </Button>
                    </div>
                  )}
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
                          <AttachmentPreview
                            ticketId={ticket.id}
                            attachmentId={a.id}
                            fileName={a.fileName}
                          />
                          <button
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
                            className="flex min-w-0 flex-1 items-center gap-3 text-left"
                          >
                            {!isImageFile(a.fileName) && (
                              <PaperclipIcon className="size-4 shrink-0 text-muted-foreground" />
                            )}
                            <div className="min-w-0 flex-1">
                              <p className="truncate text-sm font-medium">
                                {a.fileName}
                              </p>
                              <p className="text-xs text-muted-foreground">
                                {a.size > 0 ? `${formatFileSize(a.size)} · ` : ""}
                                {formatDateTime(a.uploadedAt)}
                              </p>
                            </div>
                          </button>
                          {canDeleteAttachment(a) && (
                            <button
                              type="button"
                              onClick={() => handleDeleteAttachment(a.id)}
                              className="shrink-0 rounded-md p-1.5 text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
                              aria-label={`Delete ${a.fileName}`}
                            >
                              <TrashIcon className="size-4" />
                            </button>
                          )}
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
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    variant="default"
                    onClick={handleResolve}
                    className="flex-1"
                  >
                    Resolve Ticket
                  </Button>
                  {role === "agent" && isAssignedAgent && (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => setEscalateOpen(true)}
                    >
                      Escalate
                    </Button>
                  )}
                </div>
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
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Time worked</span>
                <span>{formatDuration(ticket.timeWorkedMinutes)}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Time to close</span>
                <span>{formatDuration(ticket.timeToCloseMinutes)}</span>
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
              <AttachmentUpload files={editFiles} onChange={setEditFiles} />
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

      <Dialog open={escalateOpen} onOpenChange={setEscalateOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Escalate Ticket</DialogTitle>
            <DialogDescription>
              Return <span className="font-medium text-foreground">{ticket.reference}</span> to the open queue.
              You will be unassigned and the ticket will be available for another agent.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-2">
            <Label htmlFor="escalate-reason">Reason (optional)</Label>
            <Textarea
              id="escalate-reason"
              value={escalateReason}
              onChange={(e) => setEscalateReason(e.target.value)}
              placeholder="e.g. Out of scope, needs specialist access, cannot reproduce..."
              rows={3}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEscalateOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleEscalate} disabled={escalating}>
              {escalating ? "Escalating..." : "Escalate Ticket"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
