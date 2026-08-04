"use client"

import * as React from "react"
import { PaperclipIcon, XIcon, UploadCloud } from "lucide-react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { formatFileSize } from "@/lib/analytics"

export const ALLOWED_ATTACHMENT_EXTENSIONS = [
  ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp",
  ".pdf",
  ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt",
  ".zip",
  ".json", ".xml",
  ".mp4", ".mp3",
]

export const ALLOWED_ATTACHMENT_ACCEPT = ALLOWED_ATTACHMENT_EXTENSIONS.join(",")

export function isValidAttachmentFile(file: File, maxSizeMb: number): boolean {
  const ext = "." + (file.name.split(".").pop() || "").toLowerCase()
  if (!ALLOWED_ATTACHMENT_EXTENSIONS.includes(ext)) {
    toast.error(`"${file.name}" is not an allowed file type`, {
      description:
        "Allowed: images (png, jpg, jpeg, gif, svg, webp), pdf, documents (doc, docx, xls, xlsx, csv, txt), zip, json, xml, mp4, mp3.",
    })
    return false
  }
  if (file.size > maxSizeMb * 1024 * 1024) {
    toast.error(`"${file.name}" exceeds the ${maxSizeMb} MB limit`)
    return false
  }
  return true
}

interface AttachmentUploadProps {
  files: File[]
  onChange: (files: File[]) => void
  maxSizeMb?: number
  label?: string
}

export function AttachmentUpload({
  files,
  onChange,
  maxSizeMb = 10,
  label = "Attachments",
}: AttachmentUploadProps) {
  const inputRef = React.useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = React.useState(false)

  const addFiles = React.useCallback(
    (incoming: FileList | File[]) => {
      const next = Array.from(incoming)
      const valid = next.filter((f) => isValidAttachmentFile(f, maxSizeMb))
      if (valid.length > 0) onChange([...files, ...valid])
    },
    [files, onChange, maxSizeMb]
  )

  const removeFile = React.useCallback(
    (index: number) => {
      onChange(files.filter((_, i) => i !== index))
    },
    [files, onChange]
  )

  return (
    <div className="flex flex-col gap-2">
      <Label>{label}</Label>
      <div
        role="button"
        tabIndex={0}
        onClick={() => inputRef.current?.click()}
        onKeyDown={(e) => {
          if (e.key === "Enter" || e.key === " ") inputRef.current?.click()
        }}
        onDragOver={(e) => {
          e.preventDefault()
          setDragging(true)
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => {
          e.preventDefault()
          setDragging(false)
          if (e.dataTransfer.files.length > 0) addFiles(e.dataTransfer.files)
        }}
        className={`flex cursor-pointer flex-col items-center justify-center gap-1.5 rounded-lg border border-dashed px-4 py-5 text-center transition-colors ${
          dragging
            ? "border-primary bg-primary/5"
            : "border-border hover:bg-muted/50"
        }`}
      >
        <UploadCloud className="size-5 text-muted-foreground" />
        <p className="text-sm font-medium">Drag & drop files here</p>
        <p className="text-xs text-muted-foreground">
          or click to browse · Max {maxSizeMb} MB per file
        </p>
      </div>
      <input
        ref={inputRef}
        type="file"
        multiple
        accept={ALLOWED_ATTACHMENT_ACCEPT}
        className="hidden"
        onChange={(e) => {
          if (e.target.files?.length) {
            addFiles(e.target.files)
            e.target.value = ""
          }
        }}
      />
      {files.length > 0 && (
        <div className="mt-1 flex flex-col gap-1.5">
          {files.map((file, i) => (
            <div
              key={`${file.name}-${i}`}
              className="flex items-center justify-between rounded-md border px-3 py-1.5 text-sm"
            >
              <div className="flex min-w-0 items-center gap-2">
                <PaperclipIcon className="size-3.5 shrink-0 text-muted-foreground" />
                <span className="truncate">{file.name}</span>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                <span className="text-xs text-muted-foreground">
                  {formatFileSize(file.size)}
                </span>
                <button
                  type="button"
                  onClick={() => removeFile(i)}
                  className="text-muted-foreground hover:text-foreground"
                  aria-label={`Remove ${file.name}`}
                >
                  <XIcon className="size-3.5" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
      <div className="flex items-center gap-2">
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => inputRef.current?.click()}
        >
          <PaperclipIcon />
          Add files
        </Button>
        <span className="text-xs text-muted-foreground">Max {maxSizeMb} MB per file</span>
      </div>
    </div>
  )
}
