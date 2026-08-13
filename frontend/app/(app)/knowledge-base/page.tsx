"use client"

import * as React from "react"
import { useSearchParams } from "next/navigation"
import { Plus, Search, Pencil, Trash2, Eye, CalendarDays } from "lucide-react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Label } from "@/components/ui/label"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { useStore } from "@/lib/store"
import {
  apiGetKbArticles,
  apiGetKbArticle,
  apiCreateKbArticle,
  apiUpdateKbArticle,
  apiDeleteKbArticle,
} from "@/lib/api"
import type { KbArticleResponse } from "@/lib/api"
import { formatDate } from "@/lib/analytics"

const CATEGORIES = ["Hardware", "Software", "Network", "Access", "Other"]

const CATEGORY_ACCENT: Record<string, string> = {
  Hardware: "bg-blue-500/10 text-blue-500",
  Software: "bg-violet-500/10 text-violet-500",
  Network: "bg-emerald-500/10 text-emerald-500",
  Access: "bg-amber-500/10 text-amber-500",
  Other: "bg-slate-500/10 text-slate-500",
}

function categoryClass(category: string) {
  return CATEGORY_ACCENT[category] || CATEGORY_ACCENT.Other
}

interface EditorState {
  title: string
  excerpt: string
  body: string
  category: string
  status: "published" | "draft"
}

const EMPTY_EDITOR: EditorState = {
  title: "",
  excerpt: "",
  body: "",
  category: "Software",
  status: "published",
}

export default function KnowledgeBasePage() {
  const { role } = useStore()
  const isAdmin = role === "admin"

  const [articles, setArticles] = React.useState<KbArticleResponse[]>([])
  const [loading, setLoading] = React.useState(true)
  const [search, setSearch] = React.useState("")
  const [category, setCategory] = React.useState<string>("all")
  const [selected, setSelected] = React.useState<KbArticleResponse | null>(null)

  const [editorOpen, setEditorOpen] = React.useState(false)
  const [editing, setEditing] = React.useState<KbArticleResponse | null>(null)
  const [form, setForm] = React.useState<EditorState>(EMPTY_EDITOR)
  const [saving, setSaving] = React.useState(false)
  const [deletingId, setDeletingId] = React.useState<string | null>(null)

  const load = React.useCallback(async () => {
    setLoading(true)
    try {
      const data = await apiGetKbArticles(
        search.trim() || undefined,
        category === "all" ? undefined : category
      )
      setArticles(data.articles)
    } catch (err: any) {
      toast.error(err?.message || "Failed to load knowledge base.")
    } finally {
      setLoading(false)
    }
  }, [search, category])

  React.useEffect(() => {
    const timer = setTimeout(load, search ? 250 : 0)
    return () => clearTimeout(timer)
  }, [load, search])

  const articleId = useSearchParams().get("article")
  React.useEffect(() => {
    if (!articleId) return
    apiGetKbArticle(articleId)
      .then((article) => {
        setSelected(article)
        setArticles((prev) =>
          prev.some((a) => a.id === article.id)
            ? prev.map((a) => (a.id === article.id ? article : a))
            : prev
        )
      })
      .catch(() => {
        /* article may be a draft the user cannot see */
      })
  }, [articleId])

  async function openArticle(article: KbArticleResponse) {
    setSelected(article)
    if (!article.body) return
    try {
      const fresh = await apiGetKbArticle(article.id)
      setSelected(fresh)
      setArticles((prev) => prev.map((a) => (a.id === fresh.id ? fresh : a)))
    } catch {
      /* keep cached view */
    }
  }

  function openCreate() {
    setEditing(null)
    setForm(EMPTY_EDITOR)
    setEditorOpen(true)
  }

  function openEdit(article: KbArticleResponse) {
    setEditing(article)
    setForm({
      title: article.title,
      excerpt: article.excerpt,
      body: article.body,
      category: article.category,
      status: article.status,
    })
    setEditorOpen(true)
  }

  async function handleSave() {
    if (!form.title.trim() || !form.body.trim()) return
    setSaving(true)
    try {
      const payload = {
        title: form.title.trim(),
        excerpt: form.excerpt.trim(),
        body: form.body,
        category: form.category,
        status: form.status,
      }
      if (editing) {
        const updated = await apiUpdateKbArticle(editing.id, payload)
        toast.success("Article updated")
        setSelected((s) => (s && s.id === updated.id ? updated : s))
      } else {
        await apiCreateKbArticle(payload)
        toast.success("Article created")
      }
      setEditorOpen(false)
      await load()
    } catch (err: any) {
      toast.error(err?.message || "Failed to save article.")
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete(id: string) {
    setDeletingId(id)
    try {
      await apiDeleteKbArticle(id)
      toast.success("Article deleted")
      setSelected(null)
      await load()
    } catch (err: any) {
      toast.error(err?.message || "Failed to delete article.")
    } finally {
      setDeletingId(null)
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold tracking-tight">Knowledge Base</h2>
          <p className="text-sm text-muted-foreground">
            Self-service guides and troubleshooting articles
          </p>
        </div>
        {isAdmin && (
          <Button size="sm" onClick={openCreate}>
            <Plus data-icon="inline-start" />
            New Article
          </Button>
        )}
      </div>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="relative sm:max-w-sm flex-1">
          <Search className="absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search articles..."
            className="pl-8"
          />
        </div>
        <div className="flex gap-2 overflow-x-auto">
          {["all", ...CATEGORIES].map((c) => (
            <Button
              key={c}
              variant={category === c ? "default" : "outline"}
              size="sm"
              onClick={() => setCategory(c)}
            >
              {c === "all" ? "All" : c}
            </Button>
          ))}
        </div>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-12 text-sm text-muted-foreground">
          Loading articles...
        </div>
      ) : articles.length === 0 ? (
        <Card>
          <CardContent className="py-12 text-center text-sm text-muted-foreground">
            {search || category !== "all"
              ? "No articles match your filters."
              : "No articles yet."}
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {articles.map((article) => (
            <button
              key={article.id}
              type="button"
              onClick={() => openArticle(article)}
              className="flex flex-col rounded-lg border bg-card p-4 text-left transition-colors hover:bg-muted/50"
            >
              <div className="flex items-center justify-between gap-2">
                <Badge variant="secondary" className={categoryClass(article.category)}>
                  {article.category}
                </Badge>
                {article.status === "draft" && (
                  <Badge variant="outline">Draft</Badge>
                )}
              </div>
              <h3 className="mt-3 font-medium leading-snug">{article.title}</h3>
              <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">
                {article.excerpt || article.body}
              </p>
              <div className="mt-3 flex items-center gap-3 text-xs text-muted-foreground">
                <span className="inline-flex items-center gap-1">
                  <Eye className="size-3.5" />
                  {article.views}
                </span>
                <span className="inline-flex items-center gap-1">
                  <CalendarDays className="size-3.5" />
                  {formatDate(article.updatedAt)}
                </span>
              </div>
            </button>
          ))}
        </div>
      )}

      <Dialog open={!!selected} onOpenChange={(open) => !open && setSelected(null)}>
        <DialogContent className="sm:max-w-2xl">
          {selected && (
            <>
              <DialogHeader>
                <div className="flex items-center gap-2">
                  <Badge variant="secondary" className={categoryClass(selected.category)}>
                    {selected.category}
                  </Badge>
                  {selected.status === "draft" && <Badge variant="outline">Draft</Badge>}
                </div>
                <DialogTitle className="text-xl">{selected.title}</DialogTitle>
                <DialogDescription className="flex items-center gap-3">
                  <span className="inline-flex items-center gap-1">
                    <Eye className="size-3.5" />
                    {selected.views} views
                  </span>
                  <span>Updated {formatDate(selected.updatedAt)}</span>
                </DialogDescription>
              </DialogHeader>
              <div className="max-h-[50vh] overflow-y-auto whitespace-pre-line text-sm leading-relaxed">
                {selected.body}
              </div>
              <DialogFooter className="gap-2">
                {isAdmin && (
                  <>
                    <Button
                      variant="outline"
                      onClick={() => {
                        setSelected(null)
                        openEdit(selected)
                      }}
                    >
                      <Pencil data-icon="inline-start" />
                      Edit
                    </Button>
                    <Button
                      variant="destructive"
                      onClick={() => handleDelete(selected.id)}
                      disabled={deletingId === selected.id}
                    >
                      <Trash2 data-icon="inline-start" />
                      Delete
                    </Button>
                  </>
                )}
                <Button variant="ghost" onClick={() => setSelected(null)}>
                  Close
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={editorOpen} onOpenChange={setEditorOpen}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>{editing ? "Edit Article" : "New Article"}</DialogTitle>
            <DialogDescription>
              Write a guide to help users solve issues on their own.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="kb-title">Title</Label>
              <Input
                id="kb-title"
                value={form.title}
                onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                placeholder="e.g. How to connect to the VPN"
              />
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="flex flex-col gap-2">
                <Label>Category</Label>
                <Select
                  value={form.category}
                  onValueChange={(v) =>
                    setForm((f) => ({ ...f, category: v ?? f.category }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CATEGORIES.map((c) => (
                      <SelectItem key={c} value={c}>
                        {c}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="flex flex-col gap-2">
                <Label>Status</Label>
                <Select
                  value={form.status}
                  onValueChange={(v) =>
                    setForm((f) => ({ ...f, status: v as "published" | "draft" }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="published">Published</SelectItem>
                    <SelectItem value="draft">Draft</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="kb-excerpt">Excerpt</Label>
              <Textarea
                id="kb-excerpt"
                value={form.excerpt}
                onChange={(e) => setForm((f) => ({ ...f, excerpt: e.target.value }))}
                placeholder="Short summary shown in the article list"
                rows={2}
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="kb-body">Body</Label>
              <Textarea
                id="kb-body"
                value={form.body}
                onChange={(e) => setForm((f) => ({ ...f, body: e.target.value }))}
                placeholder="Write the step-by-step instructions..."
                rows={8}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditorOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={handleSave}
              disabled={saving || !form.title.trim() || !form.body.trim()}
            >
              {saving ? "Saving..." : editing ? "Save Changes" : "Create Article"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
