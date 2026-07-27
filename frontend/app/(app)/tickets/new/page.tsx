"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import { toast } from "sonner"
import { ArrowLeftIcon, SendIcon, PaperclipIcon, XIcon } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { useStore } from "@/lib/store"
import { apiUploadAttachment } from "@/lib/api"
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

const MAX_FILE_SIZE = 10 * 1024 * 1024

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export default function NewTicketPage() {
  const router = useRouter()
  const { createTicket } = useStore()

  const [subject, setSubject] = React.useState("")
  const [description, setDescription] = React.useState("")
  const [category, setCategory] = React.useState<TicketCategory>("")
  const [priority, setPriority] = React.useState<TicketPriority>("")
  const [files, setFiles] = React.useState<File[]>([])
  const [submitting, setSubmitting] = React.useState(false)
  const fileInputRef = React.useRef<HTMLInputElement>(null)

  const canSubmit = subject.trim() && description.trim() && category && priority

  function handleFilesSelected(e: React.ChangeEvent<HTMLInputElement>) {
    const selected = Array.from(e.target.files || [])
    const valid = selected.filter((f) => {
      if (f.size > MAX_FILE_SIZE) {
        toast.warning(`${f.name} exceeds 10 MB limit`)
        return false
      }
      return true
    })
    setFiles((prev) => [...prev, ...valid])
    if (fileInputRef.current) fileInputRef.current.value = ""
  }

  function removeFile(index: number) {
    setFiles((prev) => prev.filter((_, i) => i !== index))
  }

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

            <div className="flex flex-col gap-2">
              <Label>Attachments</Label>
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => fileInputRef.current?.click()}
                >
                  <PaperclipIcon />
                  Add files
                </Button>
                <span className="text-xs text-muted-foreground">Max 10 MB per file</span>
              </div>
              <input
                ref={fileInputRef}
                type="file"
                multiple
                className="hidden"
                onChange={handleFilesSelected}
              />
              {files.length > 0 && (
                <div className="flex flex-col gap-1.5 mt-1">
                  {files.map((file, i) => (
                    <div key={`${file.name}-${i}`} className="flex items-center justify-between rounded-md border px-3 py-1.5 text-sm">
                      <span className="truncate mr-2">{file.name}</span>
                      <div className="flex items-center gap-2 shrink-0">
                        <span className="text-muted-foreground text-xs">{formatFileSize(file.size)}</span>
                        <button type="button" onClick={() => removeFile(i)} className="text-muted-foreground hover:text-foreground">
                          <XIcon className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

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
  )
}
