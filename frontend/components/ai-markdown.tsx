import * as React from "react"
import ReactMarkdown from "react-markdown"
import remarkBreaks from "remark-breaks"
import remarkGfm from "remark-gfm"

interface AiMarkdownProps {
  text: string
  className?: string
}

export function AiMarkdown({ text, className }: AiMarkdownProps) {
  return (
    <div className={className}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm, remarkBreaks]}
        components={{
          p: ({ children }) => <p className="leading-relaxed">{children}</p>,
          strong: ({ children }) => <strong className="font-semibold">{children}</strong>,
          em: ({ children }) => <em>{children}</em>,
          code: ({ children }) => (
            <code className="rounded bg-muted px-1 py-0.5 font-mono text-[0.85em]">
              {children}
            </code>
          ),
          ul: ({ children }) => (
            <ul className="my-1 list-disc pl-5">{children}</ul>
          ),
          ol: ({ children }) => (
            <ol className="my-1 list-decimal pl-5">{children}</ol>
          ),
          li: ({ children }) => <li className="my-0.5">{children}</li>,
          h1: ({ children }) => <h1 className="text-base font-semibold">{children}</h1>,
          h2: ({ children }) => <h2 className="text-sm font-semibold">{children}</h2>,
          h3: ({ children }) => <h3 className="text-sm font-semibold">{children}</h3>,
          hr: () => <hr className="my-2 border-muted" />,
        }}
      >
        {text}
      </ReactMarkdown>
    </div>
  )
}
