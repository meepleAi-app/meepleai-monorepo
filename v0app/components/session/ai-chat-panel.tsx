"use client"

import { useState, useRef, useEffect } from "react"
import { ChevronLeft, ChevronRight, Send, Mic, Paperclip, FileText, Bot } from "lucide-react"
import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"

interface Message {
  id: string
  role: "user" | "assistant"
  content: string
  citation?: {
    page: number
    document: string
    excerpt: string
  }
}

interface AIChatPanelProps {
  gameName: string
  documentsCount: number
  isCollapsed: boolean
  onToggleCollapse: () => void
  messages: Message[]
  onSendMessage: (message: string) => void
  isTyping?: boolean
}

export function AIChatPanel({
  gameName,
  documentsCount,
  isCollapsed,
  onToggleCollapse,
  messages,
  onSendMessage,
  isTyping = false,
}: AIChatPanelProps) {
  const [inputValue, setInputValue] = useState("")
  const messagesEndRef = useRef<HTMLDivElement>(null)

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" })
  }

  useEffect(() => {
    scrollToBottom()
  }, [messages, isTyping])

  const handleSend = () => {
    if (inputValue.trim()) {
      onSendMessage(inputValue.trim())
      setInputValue("")
    }
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  if (isCollapsed) {
    return (
      <div className="hidden lg:flex flex-col items-center py-4 px-2 bg-card border-l border-border">
        <Button
          variant="ghost"
          size="icon"
          className="w-10 h-10 rounded-full mb-4"
          onClick={onToggleCollapse}
        >
          <ChevronLeft className="w-5 h-5" />
        </Button>
        <div className="flex flex-col items-center gap-2 text-muted-foreground">
          <Bot className="w-6 h-6 text-entity-agent" />
          <span className="text-xs font-body font-semibold writing-mode-vertical rotate-180" style={{ writingMode: "vertical-rl" }}>
            Assistente AI
          </span>
        </div>
      </div>
    )
  }

  return (
    <div className="hidden lg:flex flex-col w-[360px] bg-card border-l border-border">
      {/* Panel Header */}
      <div className="flex items-center justify-between p-4 border-b border-border">
        <div className="flex items-center gap-2">
          <Bot className="w-5 h-5 text-entity-agent" />
          <h3 className="font-heading font-bold text-base text-foreground">
            Assistente AI
          </h3>
        </div>
        <Button
          variant="ghost"
          size="icon"
          className="w-8 h-8"
          onClick={onToggleCollapse}
        >
          <ChevronRight className="w-4 h-4" />
        </Button>
      </div>

      {/* Game Context Chip */}
      <div className="px-4 py-2 border-b border-border/50">
        <div className="flex items-center gap-2 px-3 py-1.5 bg-muted/50 rounded-full w-fit">
          <span className="w-2 h-2 rounded-full bg-green-500" />
          <span className="text-xs font-body text-muted-foreground">
            🎲 {gameName} · {documentsCount} PDF indicizzati
          </span>
        </div>
      </div>

      {/* Messages Area */}
      <div className="flex-1 overflow-y-auto p-4 flex flex-col gap-4">
        {messages.map((message) => (
          <div
            key={message.id}
            className={cn(
              "flex flex-col gap-2",
              message.role === "user" ? "items-end" : "items-start"
            )}
          >
            <div
              className={cn(
                "max-w-[85%] px-4 py-2.5 rounded-2xl font-body text-sm",
                message.role === "user"
                  ? "bg-entity-game text-white rounded-br-md"
                  : "bg-muted border border-border rounded-bl-md"
              )}
            >
              <p className="whitespace-pre-wrap">{message.content}</p>
            </div>

            {/* Citation Card */}
            {message.citation && (
              <div className="max-w-[85%] p-3 bg-muted/50 border border-entity-document/30 rounded-lg">
                <div className="flex items-center gap-2 mb-1.5">
                  <FileText className="w-3.5 h-3.5 text-entity-document" />
                  <span className="text-xs font-body font-semibold text-entity-document">
                    pag. {message.citation.page} — {message.citation.document}
                  </span>
                </div>
                <p className="text-xs font-body text-muted-foreground italic">
                  &quot;{message.citation.excerpt}&quot;
                </p>
              </div>
            )}
          </div>
        ))}

        {/* Typing Indicator */}
        {isTyping && (
          <div className="flex items-start">
            <div className="px-4 py-3 bg-muted border border-border rounded-2xl rounded-bl-md">
              <div className="flex gap-1.5">
                <span className="w-2 h-2 bg-muted-foreground rounded-full animate-bounce" style={{ animationDelay: "0ms" }} />
                <span className="w-2 h-2 bg-muted-foreground rounded-full animate-bounce" style={{ animationDelay: "150ms" }} />
                <span className="w-2 h-2 bg-muted-foreground rounded-full animate-bounce" style={{ animationDelay: "300ms" }} />
              </div>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Input Area */}
      <div className="p-4 border-t border-border">
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="icon"
            className="w-9 h-9 flex-shrink-0 text-muted-foreground hover:text-foreground"
          >
            <Paperclip className="w-4 h-4" />
          </Button>
          <div className="flex-1 relative">
            <Input
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Chiedi qualcosa..."
              className="pr-20 bg-muted border-border font-body text-sm"
            />
            <div className="absolute right-1 top-1/2 -translate-y-1/2 flex items-center gap-1">
              <Button
                variant="ghost"
                size="icon"
                className="w-7 h-7 text-muted-foreground hover:text-foreground"
              >
                <Mic className="w-4 h-4" />
              </Button>
              <Button
                size="icon"
                className="w-7 h-7 bg-entity-game hover:bg-entity-game/90"
                onClick={handleSend}
                disabled={!inputValue.trim()}
              >
                <Send className="w-3.5 h-3.5" />
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
