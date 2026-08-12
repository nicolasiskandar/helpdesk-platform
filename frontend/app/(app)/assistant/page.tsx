"use client"

import * as React from "react"
import { useRouter, useSearchParams } from "next/navigation"
import {
  Send,
  Bot,
  User,
  Sparkles,
  FileText,
  Inbox,
  BookOpen,
  Search,
  X,
  Loader2,
} from "lucide-react"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { ScrollArea } from "@/components/ui/scroll-area"
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover"
import { AiMarkdown } from "@/components/ai-markdown"
import { useAuth } from "@/lib/auth"
import {
  apiAiStatus,
  apiChat,
  apiGetMyTickets,
  apiGetTickets,
  apiGetOpenUnassignedTickets,
  apiGetTicketById,
} from "@/lib/api"
import type { ChatMessage, TicketResponse } from "@/lib/api"
import type { Role } from "@/lib/types"

interface Message {
  id: string
  role: "user" | "assistant"
  text: string
}

const QUICK_ACTIONS = [
  {
    icon: FileText,
    label: "Create a ticket",
    href: "/tickets/new",
  },
  {
    icon: Inbox,
    label: "My tickets",
    href: "/tickets",
  },
  {
    icon: BookOpen,
    label: "Knowledge base",
    href: "/knowledge-base",
  },
]

const SUGGESTIONS = [
  "How do I connect to the VPN?",
  "What is the status of my ticket?",
  "I can't access a shared drive",
]

let idCounter = 0
const nextId = () => `msg-${++idCounter}`

function normalizeRole(role: string): Role {
  const normalized = role.toLowerCase()
  if (normalized === "admin") return "admin"
  if (normalized === "manager") return "manager"
  if (normalized === "it support agent" || normalized === "agent") return "agent"
  return "employee"
}

function statusVariant(
  statusName: string
): "default" | "secondary" | "destructive" | "outline" {
  switch (statusName) {
    case "Open":
      return "default"
    case "In Progress":
      return "secondary"
    case "Closed":
    case "Resolved by AI":
    case "Resolved - Pending Confirmation":
      return "outline"
    default:
      return "secondary"
  }
}

/** Load tickets visible to the given role */
async function loadTicketsForRole(
  role: Role,
  userId: string
): Promise<{ tickets: TicketResponse[]; sections: { label: string; tickets: TicketResponse[] }[] }> {
  if (role === "admin" || role === "manager") {
    const data = await apiGetTickets(1, 200)
    return { tickets: data.tickets, sections: [{ label: "All tickets", tickets: data.tickets }] }
  }

  if (role === "agent") {
    const [myData, openData] = await Promise.all([
      apiGetMyTickets(1, 200),
      apiGetOpenUnassignedTickets(1, 200),
    ])
    const myIds = new Set(myData.tickets.map((t) => t.id))
    // Open-unassigned tickets that aren't already in "my tickets"
    const openOnly = openData.tickets.filter((t) => !myIds.has(t.id))
    const all = [...myData.tickets, ...openOnly]
    const sections: { label: string; tickets: TicketResponse[] }[] = []
    if (myData.tickets.length > 0) sections.push({ label: "Your tickets", tickets: myData.tickets })
    if (openOnly.length > 0) sections.push({ label: "Open tickets", tickets: openOnly })
    return { tickets: all, sections }
  }

  // Employee — own tickets only
  const data = await apiGetMyTickets(1, 200)
  return { tickets: data.tickets, sections: [{ label: "Your tickets", tickets: data.tickets }] }
}

function AssistantPageContent() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const { user } = useAuth()

  const [messages, setMessages] = React.useState<Message[]>([])
  const [input, setInput] = React.useState("")
  const [thinking, setThinking] = React.useState(false)
  const [streamingId, setStreamingId] = React.useState<string | null>(null)
  const [waiting, setWaiting] = React.useState(false)
  const [live, setLive] = React.useState(false)

  const [contextTicket, setContextTicket] = React.useState<TicketResponse | null>(null)
  const [ticketSections, setTicketSections] = React.useState<{ label: string; tickets: TicketResponse[] }[]>([])
  const [allTickets, setAllTickets] = React.useState<TicketResponse[]>([])
  const [ticketsLoading, setTicketsLoading] = React.useState(true)
  const [pickerOpen, setPickerOpen] = React.useState(false)
  const [pickerQuery, setPickerQuery] = React.useState("")

  const role: Role = user ? normalizeRole(user.role) : "employee"
  const currentUserId = user?.id || ""

  // Load tickets based on role
  React.useEffect(() => {
    apiAiStatus().then(setLive)
    if (!user) return
    setTicketsLoading(true)
    loadTicketsForRole(role, currentUserId)
      .then(({ tickets, sections }) => {
        setAllTickets(tickets)
        setTicketSections(sections)
      })
      .catch(() => {
        setAllTickets([])
        setTicketSections([])
      })
      .finally(() => setTicketsLoading(false))
  }, [user, role, currentUserId])

  const autoTicketLoaded = React.useRef(false)
  React.useEffect(() => {
    if (autoTicketLoaded.current) return
    const id = searchParams.get("ticket")
    if (!id) return
    autoTicketLoaded.current = true
    apiGetTicketById(id)
      .then(setContextTicket)
      .catch(() => {})
  }, [searchParams])

  // Filter sections by search query
  const filteredSections = React.useMemo(() => {
    const q = pickerQuery.trim().toLowerCase()
    if (!q) return ticketSections
    return ticketSections
      .map((section) => ({
        ...section,
        tickets: section.tickets.filter(
          (t) =>
            t.title.toLowerCase().includes(q) ||
            t.referenceNumber.toLowerCase().includes(q) ||
            (t.description || "").toLowerCase().includes(q)
        ),
      }))
      .filter((section) => section.tickets.length > 0)
  }, [ticketSections, pickerQuery])

  const totalFiltered = React.useMemo(
    () => filteredSections.reduce((sum, s) => sum + s.tickets.length, 0),
    [filteredSections]
  )

  React.useEffect(() => {
    if (!thinking) {
      setWaiting(false)
      return
    }
    const timer = setTimeout(() => setWaiting(true), 5000)
    return () => clearTimeout(timer)
  }, [thinking, streamingId])

  const send = React.useCallback(
    async (text: string) => {
      const trimmed = text.trim()
      if (!trimmed || thinking) return
      const history: ChatMessage[] = messages
        .slice(-8)
        .map((m) => ({ role: m.role, content: m.text }))
        .filter((m) => m.content.trim().length > 0)
      setMessages((prev) => [...prev, { id: nextId(), role: "user", text: trimmed }])
      setInput("")
      setThinking(true)
      const assistantId = nextId()
      setMessages((prev) => [...prev, { id: assistantId, role: "assistant", text: "" }])
      setStreamingId(assistantId)
      try {
        await apiChat(trimmed, (token) => {
          setWaiting(false)
          setMessages((prev) =>
            prev.map((m) => (m.id === assistantId ? { ...m, text: m.text + token } : m))
          )
        }, {
          ticketId: contextTicket?.id,
          history,
        })
        setLive(true)
      } catch (err: any) {
        const warming = err?.status === 503
        setMessages((prev) =>
          prev.map((m) =>
            m.id === assistantId
              ? {
                  ...m,
                  text: warming
                    ? "The AI assistant is still warming up — its models are downloading. Give it a minute or two, then try again."
                    : err?.message
                      ? `The AI service hit a problem: ${err.message}`
                      : "I couldn't reach the AI service right now. Check the Knowledge Base for self-service guides, or open a ticket and an IT support agent will pick it up.",
                }
              : m
          )
        )
      } finally {
        setThinking(false)
        setStreamingId(null)
        setWaiting(false)
      }
    },
    [thinking, messages, contextTicket]
  )

  const greeting = user?.fullName ? `Hi ${user.fullName.split(" ")[0]}` : "Hi there"

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h2 className="text-xl font-semibold tracking-tight">AI Assistant</h2>
        <p className="text-sm text-muted-foreground">
          Get answers and take shortcuts without opening a ticket
        </p>
      </div>

      <Card className="flex min-h-[560px] flex-col">
        <CardHeader className="border-b">
          <div className="flex items-center gap-3">
            <div className="flex size-9 items-center justify-center rounded-full bg-primary/10">
              <Bot className="size-5 text-primary" />
            </div>
            <div>
              <CardTitle className="text-base">Helpdesk Assistant</CardTitle>
              <CardDescription className="flex items-center gap-2">
                <span className="relative flex size-2">
                  <span
                    className={`absolute inline-flex size-full animate-ping rounded-full opacity-60 ${
                      live ? "bg-emerald-400" : "bg-amber-400"
                    }`}
                  />
                  <span
                    className={`relative inline-flex size-2 rounded-full ${
                      live ? "bg-emerald-400" : "bg-amber-400"
                    }`}
                  />
                </span>
                {live ? "Connected" : "Service unavailable"}
              </CardDescription>
            </div>
          </div>
        </CardHeader>

        <ScrollArea className="flex-1">
          <CardContent className="flex flex-col gap-4 py-4">
            {messages.length === 0 && !thinking && (
              <div className="flex flex-col items-center gap-6 py-8 text-center">
                <div className="flex size-12 items-center justify-center rounded-full bg-primary/10">
                  <Sparkles className="size-6 text-primary" />
                </div>
                <div className="max-w-md">
                  <p className="font-medium">{greeting}! I'm your IT helpdesk assistant.</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    Ask about a problem, pick a ticket to ask about it, or jump
                    straight to a useful place.
                  </p>
                </div>
                <div className="grid w-full max-w-md gap-2 sm:grid-cols-3">
                  {QUICK_ACTIONS.map((action) => (
                    <Button
                      key={action.label}
                      variant="outline"
                      className="flex-col gap-1 py-3"
                      onClick={() => router.push(action.href)}
                    >
                      <action.icon className="size-4" />
                      <span className="text-xs font-normal">{action.label}</span>
                    </Button>
                  ))}
                </div>
                <div className="flex flex-wrap justify-center gap-2">
                  {SUGGESTIONS.map((s) => (
                    <button
                      key={s}
                      type="button"
                      onClick={() => send(s)}
                      className="rounded-full border px-3 py-1 text-xs text-muted-foreground transition-colors hover:bg-muted/50"
                    >
                      {s}
                    </button>
                  ))}
                </div>
              </div>
            )}

            {contextTicket && (
              <div className="flex items-center gap-2 rounded-lg border bg-muted/40 px-3 py-2 text-sm">
                <FileText className="size-4 shrink-0 text-primary" />
                <span className="truncate font-medium">
                  Asking about {contextTicket.referenceNumber} — {contextTicket.title}
                </span>
                <button
                  type="button"
                  aria-label="Clear ticket context"
                  onClick={() => setContextTicket(null)}
                  className="ml-auto rounded-full p-0.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                >
                  <X className="size-4" />
                </button>
              </div>
            )}

            {messages.map((m) => (
              <div
                key={m.id}
                className={`flex gap-2 ${m.role === "user" ? "justify-end" : "justify-start"}`}
              >
                {m.role === "assistant" && (
                  <div className="mt-1 flex size-7 shrink-0 items-center justify-center rounded-full bg-primary/10">
                    <Bot className="size-4 text-primary" />
                  </div>
                )}
                <div
                  className={`max-w-[80%] whitespace-pre-line rounded-lg px-3.5 py-2 text-sm ${
                    m.role === "user"
                      ? "bg-primary text-primary-foreground"
                      : "border bg-muted/50"
                  }`}
                >
                  {m.role === "assistant" ? (
                    <AiMarkdown text={m.text || "…"} />
                  ) : (
                    (m.text || "…")
                  )}
                </div>
                {m.role === "user" && (
                  <div className="mt-1 flex size-7 shrink-0 items-center justify-center rounded-full bg-muted">
                    <User className="size-4 text-muted-foreground" />
                  </div>
                )}
              </div>
            ))}

            {thinking && !streamingId && (
              <div className="flex items-center gap-2">
                <div className="flex size-7 items-center justify-center rounded-full bg-primary/10">
                  <Bot className="size-4 text-primary" />
                </div>
                <div className="rounded-lg border bg-muted/50 px-3.5 py-2 text-sm text-muted-foreground">
                  Thinking<span className="animate-pulse">…</span>
                </div>
              </div>
            )}

            {waiting && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Bot className="size-4 shrink-0 animate-pulse text-primary" />
                <span>
                  Model is warming up — the first reply can take up to a minute…
                </span>
              </div>
            )}
          </CardContent>
        </ScrollArea>

        <div className="border-t p-3">
          <div className="mb-2 flex items-center gap-2">
            {contextTicket ? (
              <Badge variant="secondary" className="max-w-full gap-1 pr-1">
                <FileText className="size-3 shrink-0" />
                <span className="truncate">
                  {contextTicket.referenceNumber} — {contextTicket.title}
                </span>
                <button
                  type="button"
                  aria-label="Clear ticket context"
                  onClick={() => setContextTicket(null)}
                  className="ml-1 rounded-full p-0.5 hover:bg-muted"
                >
                  <X className="size-3" />
                </button>
              </Badge>
            ) : (
              <Popover open={pickerOpen} onOpenChange={(open) => {
                setPickerOpen(open)
                if (!open) setPickerQuery("")
              }}>
                <PopoverTrigger asChild>
                  <Button type="button" variant="outline" size="sm" className="gap-1.5">
                    <FileText className="size-3.5" />
                    Ask about a ticket
                  </Button>
                </PopoverTrigger>
                <PopoverContent align="start" className="w-96 p-0" sideOffset={8}>
                  <div className="border-b p-2">
                    <div className="relative">
                      <Search className="absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                      <Input
                        value={pickerQuery}
                        onChange={(e) => setPickerQuery(e.target.value)}
                        placeholder={
                          role === "admin" || role === "manager"
                            ? "Search all tickets…"
                            : role === "agent"
                              ? "Search your & open tickets…"
                              : "Search your tickets…"
                        }
                        className="pl-8"
                        autoFocus
                      />
                    </div>
                  </div>
                  <div className="max-h-80 overflow-y-auto overscroll-contain">
                    {ticketsLoading ? (
                      <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
                        <Loader2 className="size-4 animate-spin" />
                        Loading tickets…
                      </div>
                    ) : totalFiltered === 0 ? (
                      <div className="px-3 py-8 text-center text-sm text-muted-foreground">
                        {allTickets.length === 0
                          ? "No tickets available."
                          : "No tickets match your search."}
                      </div>
                    ) : (
                      <div className="p-1">
                        {filteredSections.map((section) => (
                          <div key={section.label}>
                            {filteredSections.length > 1 && (
                              <div className="px-2 pb-1 pt-2 text-xs font-medium text-muted-foreground">
                                {section.label}
                              </div>
                            )}
                            {section.tickets.map((t) => (
                              <button
                                key={t.id}
                                type="button"
                                onClick={() => {
                                  setContextTicket(t)
                                  setPickerOpen(false)
                                  setPickerQuery("")
                                }}
                                className="flex w-full items-start gap-2 rounded-md px-2 py-2 text-left text-sm transition-colors hover:bg-muted"
                              >
                                <FileText className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                                <div className="flex min-w-0 flex-1 flex-col gap-0.5">
                                  <div className="flex items-center gap-2">
                                    <span className="truncate font-medium">
                                      {t.referenceNumber}
                                    </span>
                                    <Badge
                                      variant={statusVariant(t.statusName)}
                                      className="shrink-0 text-[10px] px-1.5 py-0"
                                    >
                                      {t.statusName}
                                    </Badge>
                                  </div>
                                  <span className="truncate text-muted-foreground">
                                    {t.title}
                                  </span>
                                  <span className="text-xs text-muted-foreground/70">
                                    {t.categoryName} · {t.priorityName}
                                  </span>
                                </div>
                              </button>
                            ))}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </PopoverContent>
              </Popover>
            )}
          </div>
          <form
            className="flex gap-2"
            onSubmit={(e) => {
              e.preventDefault()
              send(input)
            }}
          >
            <Input
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder="Describe your problem or ask a question…"
              disabled={thinking}
            />
            <Button type="submit" size="icon" disabled={thinking || !input.trim()}>
              <Send className="size-4" />
            </Button>
          </form>
          <div className="mt-2">
            <Badge variant="outline" className="text-muted-foreground">
              {live
                ? "Streaming answers from the AI service"
                : "AI service is offline — answers may not be available"}
            </Badge>
          </div>
        </div>
      </Card>
    </div>
  )
}

export default function AssistantPage() {
  return (
    <React.Suspense fallback={null}>
      <AssistantPageContent />
    </React.Suspense>
  )
}
