"use client"

import * as React from "react"
import { useRouter } from "next/navigation"
import { BookOpen, Loader2, Search, Ticket as TicketIcon } from "lucide-react"

import { Popover, PopoverContent } from "@/components/ui/popover"
import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
} from "@/components/ui/input-group"
import { cn } from "@/lib/utils"
import {
  apiGetKbArticles,
  apiSearchTickets,
  type KbArticleResponse,
  type SearchTicketResult,
} from "@/lib/api"

interface SearchItem {
  key: string
  kind: "ticket" | "article"
  title: string
  subtitle: string
  href: string
}

const MIN_QUERY_LENGTH = 2

export function GlobalSearch() {
  const router = useRouter()
  const inputRef = React.useRef<HTMLInputElement>(null)
  const [query, setQuery] = React.useState("")
  const [dismissed, setDismissed] = React.useState(false)
  const [tickets, setTickets] = React.useState<SearchTicketResult[]>([])
  const [articles, setArticles] = React.useState<KbArticleResponse[]>([])
  const [loading, setLoading] = React.useState(false)
  const [failed, setFailed] = React.useState(false)
  const [highlighted, setHighlighted] = React.useState(-1)
  const requestIdRef = React.useRef(0)

  React.useEffect(() => {
    const q = query.trim()
    if (q.length < MIN_QUERY_LENGTH) {
      setTickets([])
      setArticles([])
      setLoading(false)
      setFailed(false)
      return
    }

    const requestId = ++requestIdRef.current
    setLoading(true)
    const timer = setTimeout(async () => {
      const [ticketResult, articleResult] = await Promise.allSettled([
        apiSearchTickets(q),
        apiGetKbArticles(q, undefined, 1, 5),
      ])
      if (requestIdRef.current !== requestId) return
      setLoading(false)
      setFailed(
        ticketResult.status === "rejected" && articleResult.status === "rejected"
      )
      setTickets(ticketResult.status === "fulfilled" ? ticketResult.value.items : [])
      setArticles(articleResult.status === "fulfilled" ? articleResult.value.articles : [])
    }, 300)
    return () => clearTimeout(timer)
  }, [query])

  const items = React.useMemo<SearchItem[]>(() => {
    const list: SearchItem[] = []
    for (const ticket of tickets) {
      list.push({
        key: `ticket-${ticket.ticketId}`,
        kind: "ticket",
        title: ticket.title,
        subtitle: [ticket.referenceNumber, ticket.category, ticket.priority]
          .filter(Boolean)
          .join(" • "),
        href: `/tickets/${ticket.ticketId}`,
      })
    }
    for (const article of articles) {
      list.push({
        key: `article-${article.id}`,
        kind: "article",
        title: article.title,
        subtitle: article.category,
        href: `/knowledge-base?article=${article.id}`,
      })
    }
    return list
  }, [tickets, articles])

  React.useEffect(() => {
    setHighlighted(items.length > 0 ? 0 : -1)
  }, [items])

  const open = query.trim().length >= MIN_QUERY_LENGTH && !dismissed

  function select(index: number) {
    const target = items[index]
    if (!target) return
    setQuery("")
    router.push(target.href)
  }

  function handleChange(value: string) {
    setQuery(value)
    setDismissed(false)
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.nativeEvent.isComposing) return
    if (event.key === "Escape") {
      setQuery("")
      return
    }
    if (event.key === "ArrowDown") {
      event.preventDefault()
      setHighlighted((current) =>
        items.length > 0 ? (current + 1) % items.length : -1
      )
      return
    }
    if (event.key === "ArrowUp") {
      event.preventDefault()
      setHighlighted((current) =>
        items.length > 0 ? (current - 1 + items.length) % items.length : -1
      )
      return
    }
    if (event.key === "Enter") {
      event.preventDefault()
      select(highlighted >= 0 ? highlighted : 0)
    }
  }

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        if (!next) setDismissed(true)
      }}
    >
      <InputGroup className="w-64">
        <InputGroupInput
          ref={inputRef}
          placeholder="Search tickets, articles..."
          aria-label="Search"
          value={query}
          onChange={(event) => handleChange(event.target.value)}
          onFocus={() => setDismissed(false)}
          onKeyDown={handleKeyDown}
        />
        <InputGroupAddon>
          <Search />
        </InputGroupAddon>
      </InputGroup>
      <PopoverContent
        anchor={inputRef}
        initialFocus={false}
        align="start"
        side="bottom"
        sideOffset={6}
        className="w-80 p-1"
      >
        {loading ? (
          <div className="flex items-center gap-2 px-2 py-3 text-xs text-muted-foreground">
            <Loader2 className="size-3.5 animate-spin" />
            Searching…
          </div>
        ) : failed ? (
          <div className="px-2 py-3 text-center text-xs text-muted-foreground">
            Search is unavailable right now.
          </div>
        ) : items.length === 0 ? (
          <div className="px-2 py-3 text-center text-xs text-muted-foreground">
            No results found.
          </div>
        ) : (
          <div className="max-h-96 overflow-y-auto">
            {items.map((item, index) => {
              const showHeader =
                item.kind === "ticket"
                  ? index === 0
                  : tickets.length > 0
              return (
                <React.Fragment key={item.key}>
                  {showHeader ? (
                    <div className="px-2 pb-1 pt-2 text-[11px] font-medium tracking-wide text-muted-foreground uppercase first:pt-1">
                      {item.kind === "ticket" ? "Tickets" : "Articles"}
                    </div>
                  ) : null}
                  <button
                    type="button"
                    onMouseEnter={() => setHighlighted(index)}
                    onClick={() => select(index)}
                    className={cn(
                      "flex w-full items-start gap-2.5 rounded-md px-2 py-2 text-left hover:bg-muted",
                      highlighted === index && "bg-muted"
                    )}
                  >
                    {item.kind === "ticket" ? (
                      <TicketIcon className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                    ) : (
                      <BookOpen className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                    )}
                    <div className="min-w-0 flex-1">
                      <div className="truncate text-sm font-medium">
                        {item.title}
                      </div>
                      <div className="truncate text-xs text-muted-foreground">
                        {item.subtitle}
                      </div>
                    </div>
                  </button>
                </React.Fragment>
              )
            })}
          </div>
        )}
      </PopoverContent>
    </Popover>
  )
}
