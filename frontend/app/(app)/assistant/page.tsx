"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import { Send, Bot, User, Sparkles, FileText, Inbox, BookOpen } from "lucide-react"

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
import { useAuth } from "@/lib/auth"

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

export default function AssistantPage() {
  const router = useRouter()
  const { user } = useAuth()

  const [messages, setMessages] = React.useState<Message[]>([])
  const [input, setInput] = React.useState("")
  const [thinking, setThinking] = React.useState(false)

  const send = React.useCallback(
    (text: string) => {
      const trimmed = text.trim()
      if (!trimmed || thinking) return
      setMessages((prev) => [...prev, { id: nextId(), role: "user", text: trimmed }])
      setInput("")
      setThinking(true)
      setTimeout(() => {
        setMessages((prev) => [
          ...prev,
          {
            id: nextId(),
            role: "assistant",
            text:
              "The AI assistant isn't reachable right now, so I can't look up live information yet. " +
              "In the meantime you can check the Knowledge Base for self-service guides, or open a ticket " +
              "and an IT support agent will pick it up. I'll be able to answer directly once the AI service is online.",
          },
        ])
        setThinking(false)
      }, 900)
    },
    [thinking]
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
                  <span className="absolute inline-flex size-full animate-ping rounded-full bg-amber-400 opacity-60" />
                  <span className="relative inline-flex size-2 rounded-full bg-amber-400" />
                </span>
                Service unavailable
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
                    Ask about a problem, or jump straight to a useful place.
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
                  {m.text}
                </div>
                {m.role === "user" && (
                  <div className="mt-1 flex size-7 shrink-0 items-center justify-center rounded-full bg-muted">
                    <User className="size-4 text-muted-foreground" />
                  </div>
                )}
              </div>
            ))}

            {thinking && (
              <div className="flex items-center gap-2">
                <div className="flex size-7 items-center justify-center rounded-full bg-primary/10">
                  <Bot className="size-4 text-primary" />
                </div>
                <div className="rounded-lg border bg-muted/50 px-3.5 py-2 text-sm text-muted-foreground">
                  Thinking<span className="animate-pulse">…</span>
                </div>
              </div>
            )}
          </CardContent>
        </ScrollArea>

        <div className="border-t p-3">
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
              AI service is offline — answers are simulated for now
            </Badge>
          </div>
        </div>
      </Card>
    </div>
  )
}
