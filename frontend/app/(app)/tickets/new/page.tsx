"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import Link from "next/link"
import { toast } from "sonner"
import { ArrowLeftIcon, SendIcon, SparklesIcon } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Label } from "@/components/ui/label"
import { RoleGuard } from "@/components/role-guard"
import { AttachmentUpload } from "@/components/attachment-upload"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { useStore } from "@/lib/store"
import { apiUploadAttachment, apiSimilarTickets, apiAnalyzeTicket } from "@/lib/api"
import type { SimilarTicketResponse } from "@/lib/api"
import type { TicketCategory, TicketPriority } from "@/lib/types"

const CATEGORIES: { value: TicketCategory; label: string }[] = [
  { value: "Hardware", label: "Hardware" },
  { value: "Software", label: "Software" },
  { value: "Network", label: "Network" },
  { value: "Access", label: "Access" },
  { value: "Other", label: "Other" },
]

const PRIORITIES: { value: TicketPriority; label: string }[] = [
  { value: "Low", label: "Low" },
  { value: "Medium", label: "Medium" },
  { value: "High", label: "High" },
  { value: "Critical", label: "Critical" },
]

export default function NewTicketPage() {
  const router = useRouter()
  const { createTicket } = useStore()

  const [subject, setSubject] = React.useState("")
  const [description, setDescription] = React.useState("")
  const [category, setCategory] = React.useState<TicketCategory>("")
  const [priority, setPriority] = React.useState<TicketPriority>("")
  const [files, setFiles] = React.useState<File[]>([])
  const [submitting, setSubmitting] = React.useState(false)

  const [similarStatus, setSimilarStatus] = React.useState<
    "idle" | "loading" | "results" | "empty" | "error"
  >("idle")
  const [similarTickets, setSimilarTickets] = React.useState<SimilarTicketResponse[]>([])
  const similarRequestRef = React.useRef(0)
  const aiPrefilledRef = React.useRef(false)

  const canSubmit = subject.trim() && description.trim() && category && priority

  React.useEffect(() => {
    const requestId = ++similarRequestRef.current
    if (!description.trim()) {
      setSimilarTickets([])
      setSimilarStatus("idle")
      return
    }
    const query = `${subject} ${description}`.trim()
    const t = window.setTimeout(async () => {
      setSimilarStatus("loading")
      try {
        const similar = await apiSimilarTickets(query)
        if (requestId !== similarRequestRef.current) return
        const results = similar.slice(0, 5)
        setSimilarTickets(results)
        setSimilarStatus(results.length > 0 ? "results" : "empty")
      } catch {
        if (requestId !== similarRequestRef.current) return
        setSimilarTickets([])
        setSimilarStatus("error")
      }
    }, 600)
    return () => window.clearTimeout(t)
  }, [subject, description])

  React.useEffect(() => {
    if (!subject.trim() || !description.trim()) return
    const t = window.setTimeout(async () => {
      try {
        const analysis = await apiAnalyzeTicket(subject, description)
        if (!aiPrefilledRef.current) {
          setCategory((prev) => (prev || analysis.category) as TicketCategory)
          setPriority((prev) => (prev || analysis.priority) as TicketPriority)
          aiPrefilledRef.current = true
        }
      } catch {
        /* AI unavailable — leave manual selection */
      }
    }, 900)
    return () => window.clearTimeout(t)
  }, [subject, description])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!canSubmit || submitting) return

    setSubmitting(true)
    try {
      const ticket = await createTicket({ subject, description, category, priority })

      for (const file of files) {
        try {
          await apiUploadAttachment(ticket.id, file)
        } catch (err: any) {
          toast.error(`Failed to upload ${file.name}`, {
            description: err?.message || "Upload failed",
          })
        }
      }

      toast.success("Ticket created", {
        description: `${ticket.reference} — ${ticket.subject}`,
      })
      router.push(`/tickets/${ticket.id}`)
    } catch (err: any) {
      toast.error("Failed to create ticket", {
        description: err?.message || "Please try again.",
      })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <RoleGuard allowedRoles={["admin", "employee"]}>
      <div className="flex flex-col gap-6">
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon-sm"
          onClick={() => router.back()}
        >
          <ArrowLeftIcon />
        </Button>
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold tracking-tight text-balance">
            New Ticket
          </h1>
          <p className="text-sm text-muted-foreground">
            Describe your issue and we&apos;ll get it to the right team.
          </p>
        </div>
      </div>

      <Card className="max-w-2xl">
        <CardHeader>
          <CardTitle>Ticket Details</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-5">
            <div className="flex flex-col gap-2">
              <Label htmlFor="subject">Subject</Label>
              <Input
                id="subject"
                placeholder="Brief summary of the issue"
                value={subject}
                onChange={(e) => setSubject(e.target.value)}
                maxLength={200}
              />
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="description">Description</Label>
              <Textarea
                id="description"
                placeholder="Provide as much detail as possible..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                rows={6}
              />
            </div>

            {(similarStatus === "loading" ||
              similarStatus === "results" ||
              similarStatus === "empty" ||
              similarStatus === "error") && (
              <div className="flex flex-col gap-3 rounded-lg border bg-muted/40 p-4">
                <div className="flex items-center gap-2 text-sm font-medium">
                  <SparklesIcon className="h-4 w-4 text-primary" />
                  Similar resolved tickets
                </div>
                {similarStatus === "loading" ? (
                  <p className="text-sm text-muted-foreground">
                    Searching the knowledge base...
                  </p>
                ) : similarStatus === "results" ? (
                  <ul className="flex flex-col gap-2">
                    {similarTickets.map((t) => (
                      <li key={t.ticketId}>
                        <Link
                          href={`/tickets/${t.ticketId}`}
                          className="group flex flex-col gap-0.5 rounded-md p-2 transition-colors hover:bg-background"
                        >
                          <span className="text-sm font-medium group-hover:underline">
                            {t.title}
                          </span>
                          <span className="text-xs text-muted-foreground">
                            {t.referenceNumber} · {t.status} ·{" "}
                            {Math.round(t.score * 100)}% match
                          </span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                ) : similarStatus === "empty" ? (
                  <p className="text-sm text-muted-foreground">
                    No similar tickets found.
                  </p>
                ) : (
                  <p className="text-sm text-muted-foreground">
                    Similar-ticket search is currently unavailable.
                  </p>
                )}
              </div>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="flex flex-col gap-2">
                <Label>Category</Label>
                <Select value={category} onValueChange={(v) => setCategory(v as TicketCategory)}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select category" />
                  </SelectTrigger>
                  <SelectContent>
                    {CATEGORIES.map((c) => (
                      <SelectItem key={c.value} value={c.value}>
                        {c.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="flex flex-col gap-2">
                <Label>Priority</Label>
                <Select value={priority} onValueChange={(v) => setPriority(v as TicketPriority)}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select priority" />
                  </SelectTrigger>
                  <SelectContent>
                    {PRIORITIES.map((p) => (
                      <SelectItem key={p.value} value={p.value}>
                        {p.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <AttachmentUpload files={files} onChange={setFiles} />

            <div className="flex justify-end gap-3 pt-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => router.back()}
                disabled={submitting}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={!canSubmit || submitting}>
                {submitting ? "Creating..." : "Create Ticket"}
                {!submitting && <SendIcon data-icon="inline-end" />}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
      </div>
    </RoleGuard>
  )
}
