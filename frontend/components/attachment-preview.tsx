"use client"

import * as React from "react"
import { FileImage } from "lucide-react"

import {
  Dialog,
  DialogContent,
  DialogTitle,
} from "@/components/ui/dialog"
import { apiAttachmentDownloadUrl } from "@/lib/api"

const IMAGE_EXTENSIONS = [".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp"]

export function isImageFile(fileName: string): boolean {
  const ext = "." + (fileName.split(".").pop() || "").toLowerCase()
  return IMAGE_EXTENSIONS.includes(ext)
}

export function AttachmentPreview({
  ticketId,
  attachmentId,
  fileName,
}: {
  ticketId: string
  attachmentId: string
  fileName: string
}) {
  const [url, setUrl] = React.useState<string | null>(null)
  const [open, setOpen] = React.useState(false)

  React.useEffect(() => {
    let objectUrl: string | null = null
    let cancelled = false
    fetch(apiAttachmentDownloadUrl(ticketId, attachmentId), {
      headers: {
        Authorization: `Bearer ${sessionStorage.getItem("accessToken") || ""}`,
      },
    })
      .then((res) => (res.ok ? res.blob() : null))
      .then((blob) => {
        if (!blob || cancelled) return
        objectUrl = URL.createObjectURL(blob)
        setUrl(objectUrl)
      })
      .catch(() => {
        /* preview unavailable */
      })
    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [ticketId, attachmentId])

  if (!isImageFile(fileName)) return null

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="shrink-0 overflow-hidden rounded-md border"
        aria-label={`Preview ${fileName}`}
      >
        {url ? (
          <img
            src={url}
            alt={fileName}
            className="size-16 object-cover transition-transform hover:scale-105"
          />
        ) : (
          <div className="flex size-16 items-center justify-center bg-muted text-muted-foreground">
            <FileImage className="size-5" />
          </div>
        )}
      </button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-3xl">
          <DialogTitle className="sr-only">{fileName}</DialogTitle>
          {url ? (
            <img
              src={url}
              alt={fileName}
              className="mx-auto max-h-[70vh] w-auto rounded-md"
            />
          ) : null}
        </DialogContent>
      </Dialog>
    </>
  )
}
