"use client"

import * as React from "react"
import { File, FileArchive, FileAudio, FileImage, FileText, FileVideo } from "lucide-react"

import {
  Dialog,
  DialogContent,
  DialogTitle,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { downloadFile } from "@/lib/api"
import { formatFileSize } from "@/lib/analytics"

const IMAGE_EXTENSIONS = [".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp"]

export function isImageFile(fileName: string): boolean {
  const ext = "." + (fileName.split(".").pop() || "").toLowerCase()
  return IMAGE_EXTENSIONS.includes(ext)
}

type FileKind = "image" | "pdf" | "video" | "audio" | "other"

function fileKind(fileName: string): FileKind {
  const ext = "." + (fileName.split(".").pop() || "").toLowerCase()
  if (IMAGE_EXTENSIONS.includes(ext)) return "image"
  if (ext === ".pdf") return "pdf"
  if (ext === ".mp4" || ext === ".mov" || ext === ".webm") return "video"
  if (ext === ".mp3" || ext === ".wav" || ext === ".ogg") return "audio"
  return "other"
}

function FileTypeIcon({ fileName, className }: { fileName: string; className?: string }) {
  const kind = fileKind(fileName)
  switch (kind) {
    case "pdf":
      return <FileText className={className} />
    case "video":
      return <FileVideo className={className} />
    case "audio":
      return <FileAudio className={className} />
    case "image":
      return <FileImage className={className} />
    case "other":
      return fileName.toLowerCase().endsWith(".zip") ? <FileArchive className={className} /> : <File className={className} />
  }
}

function useAttachmentBlob(url: string) {
  const [blobUrl, setBlobUrl] = React.useState<string | null>(null)

  const load = React.useCallback(async () => {
    try {
      const res = await fetch(url, {
        headers: {
          Authorization: `Bearer ${sessionStorage.getItem("accessToken") || ""}`,
        },
      })
      if (!res.ok) return
      const blob = await res.blob()
      const next = URL.createObjectURL(blob)
      setBlobUrl((prev) => {
        if (prev) URL.revokeObjectURL(prev)
        return next
      })
    } catch {
      /* preview unavailable */
    }
  }, [url])

  return { blobUrl, setBlobUrl, load }
}

export function AttachmentPreview({
  url,
  fileName,
  size,
}: {
  url: string
  fileName: string
  size?: number
}) {
  const kind = fileKind(fileName)
  const isImage = kind === "image"
  const [open, setOpen] = React.useState(false)
  const [loading, setLoading] = React.useState(false)
  const { blobUrl, setBlobUrl, load } = useAttachmentBlob(url)

  React.useEffect(() => {
    if (!isImage) return
    let objectUrl: string | null = null
    let cancelled = false
    fetch(url, {
      headers: {
        Authorization: `Bearer ${sessionStorage.getItem("accessToken") || ""}`,
      },
    })
      .then((res) => (res.ok ? res.blob() : null))
      .then((blob) => {
        if (!blob || cancelled) return
        objectUrl = URL.createObjectURL(blob)
        setBlobUrl((prev) => {
          if (prev && prev !== objectUrl) URL.revokeObjectURL(prev)
          return objectUrl
        })
      })
      .catch(() => {})
    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [url, isImage, setBlobUrl])

  const openPreview = React.useCallback(async () => {
    setOpen(true)
    if (kind !== "image" && kind !== "other" && !blobUrl) {
      setLoading(true)
      await load()
      setLoading(false)
    }
  }, [kind, blobUrl, load])

  return (
    <>
      {isImage ? (
        <button
          type="button"
          onClick={openPreview}
          className="shrink-0 overflow-hidden rounded-md border transition-opacity hover:opacity-90"
          aria-label={`Preview ${fileName}`}
        >
          {blobUrl ? (
            <img
              src={blobUrl}
              alt={fileName}
              className="size-16 object-cover transition-transform hover:scale-105"
            />
          ) : (
            <div className="flex size-16 items-center justify-center bg-muted text-muted-foreground">
              <FileImage className="size-5" />
            </div>
          )}
        </button>
      ) : (
        <button
          type="button"
          onClick={openPreview}
          className="flex shrink-0 items-center gap-2 rounded-md border bg-muted/40 px-2.5 py-2 text-xs transition-colors hover:bg-muted/70"
          aria-label={`Preview ${fileName}`}
        >
          <FileTypeIcon fileName={fileName} className="size-4 shrink-0 text-muted-foreground" />
          <span className="max-w-40 truncate font-medium">{fileName}</span>
          {typeof size === "number" && size > 0 && (
            <span className="shrink-0 text-muted-foreground">{formatFileSize(size)}</span>
          )}
        </button>
      )}
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className={kind === "image" || kind === "pdf" ? "sm:max-w-4xl" : "sm:max-w-md"}>
          <DialogTitle className="sr-only">{fileName}</DialogTitle>
          {kind === "image" && blobUrl ? (
            <img
              src={blobUrl}
              alt={fileName}
              className="mx-auto max-h-[75vh] w-auto rounded-md"
            />
          ) : kind === "pdf" && blobUrl ? (
            <iframe
              src={blobUrl}
              title={fileName}
              className="h-[75vh] w-full rounded-md border"
            />
          ) : kind === "video" && blobUrl ? (
            <video src={blobUrl} controls className="mx-auto max-h-[75vh] w-full rounded-md" />
          ) : kind === "audio" && blobUrl ? (
            <audio src={blobUrl} controls className="w-full" />
          ) : kind === "other" ? (
            <div className="flex flex-col items-center gap-3 py-6 text-center">
              <FileTypeIcon fileName={fileName} className="size-12 text-muted-foreground" />
              <div>
                <p className="break-all text-sm font-medium">{fileName}</p>
                {typeof size === "number" && size > 0 && (
                  <p className="text-xs text-muted-foreground">{formatFileSize(size)}</p>
                )}
              </div>
              <Button
                type="button"
                onClick={async () => {
                  await downloadFile(url, fileName)
                }}
              >
                Download
              </Button>
            </div>
          ) : (
            <div className="flex items-center gap-3 py-6 text-center text-sm text-muted-foreground">
              {loading ? "Loading preview..." : "Preview unavailable."}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </>
  )
}
