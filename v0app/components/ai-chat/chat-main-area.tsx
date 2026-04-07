"use client"

import { useState, useRef, useEffect } from "react"
import { Send, Mic, Paperclip, FileText, Bot, ChevronDown, Search } from "lucide-react"
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

interface GameContext {
  id: string
  name: string
  emoji: string
  pdfCount: number
}

interface ChatMainAreaProps {
  gameContext: GameContext | null
  messages: Message[]
  onSendMessage: (message: string) => void
  onSelectGame: () => void
  isTyping?: boolean
}

const suggestedQuestions = [
  "Qual è la regola del commercio?",
  "Come si vince?",
  "Quante risorse si pescano?",
  "Spiega le carte sviluppo",
]

export function ChatMainArea({
  gameContext,
  messages,
  onSendMessage,
  onSelectGame,
  isTyping = false,
}: ChatMainAreaProps) {
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

  const handleSuggestedQuestion = (question: string) => {
    onSendMessage(question)
  }

  const isEmpty = messages.length === 0

  return (
    <div className="flex-1 flex flex-col h-[calc(100vh-52px)] bg-background">
      {/* Chat Header */}
      <div className="flex items-center justify-between px-6 py-3 border-b border-border">
        {gameContext ? (
          <button
            onClick={onSelectGame}
            className="flex items-center gap-2 px-3 py-1.5 bg-muted rounded-full hover:bg-muted/80 transition-colors"
          >
            <span className="text-base">{gameContext.emoji}</span>
            <span className="font-body text-sm text-foreground">
              {gameContext.name}
            </span>
            <span className="font-body text-xs text-muted-foreground">
              · {gameContext.pdfCount} PDF
            </span>
            <ChevronDown className="w-3.5 h-3.5 text-muted-foreground" />
          </button>
        ) : (
          <button
            onClick={onSelectGame}
            className="flex items-center gap-2 px-3 py-1.5 bg-muted/50 border border-dashed border-border rounded-full hover:bg-muted transition-colors"
          >
            <Search className="w-4 h-4 text-muted-foreground" />
            <span className="font-body text-sm text-muted-foreground">
              Nessun gioco selezionato
            </span>
          </button>
        )}
      </div>

      {/* Messages Area */}
      <div className="flex-1 overflow-y-auto p-6">
        {isEmpty ? (
          /* Welcome State */
          <div className="flex flex-col items-center justify-center h-full max-w-md mx-auto text-center">
            {/* AI Avatar */}
            <div className="w-14 h-14 rounded-2xl bg-gradient-to-br from-entity-player to-entity-player/70 flex items-center justify-center mb-4 shadow-warm">
              <Bot className="w-7 h-7 text-white" />
            </div>

            {/* Welcome Text */}
            <h2 className="font-heading font-bold text-xl text-foreground mb-2">
              Ciao! Sono il tuo assistente AI
            </h2>
            <p className="font-body text-sm text-muted-foreground mb-6">
              Chiedimi qualsiasi cosa sui regolamenti dei tuoi giochi da tavolo.
            </p>

            {/* Suggested Questions */}
            <div className="flex flex-wrap gap-2 justify-center">
              {suggestedQuestions.map((question) => (
                <button
                  key={question}
                  onClick={() => handleSuggestedQuestion(question)}
                  className="px-4 py-2 bg-muted border border-border rounded-full font-body text-sm text-foreground hover:bg-muted/80 hover:border-entity-game/50 transition-colors"
                >
                  {question}
                </button>
              ))}
            </div>
          </div>
        ) : (
          /* Messages */
          <div className="max-w-3xl mx-auto flex flex-col gap-4">
            {messages.map((message) => (
              <div
                key={message.id}
                className={cn(
                  "flex flex-col gap-2",
                  message.role === "user" ? "items-end" : "items-start"
                )}
              >
                {/* Message Bubble */}
                <div
                  className={cn(
                    "max-w-[80%] px-4 py-3 rounded-2xl font-body text-sm leading-relaxed",
                    message.role === "user"
                      ? "bg-[rgba(180,60,0,0.3)] border border-entity-game/50 text-foreground rounded-br-md"
                      : "bg-card border border-border rounded-bl-md"
                  )}
                >
                  {message.role === "assistant" ? (
                    <div 
                      className="prose prose-invert prose-sm max-w-none"
                      dangerouslySetInnerHTML={{ __html: formatMarkdown(message.content) }}
                    />
                  ) : (
                    <p className="whitespace-pre-wrap">{message.content}</p>
                  )}
                </div>

                {/* Citation Card */}
                {message.citation && (
                  <div className="max-w-[80%] p-3 bg-muted/50 border border-entity-document/30 rounded-lg">
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
                <div className="px-4 py-3 bg-card border border-border rounded-2xl rounded-bl-md">
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
        )}
      </div>

      {/* Input Area */}
      <div className="border-t border-border p-4 bg-background">
        <div className="max-w-3xl mx-auto flex items-center gap-3">
          {/* Game Context Selector (Mobile shows on desktop too) */}
          {gameContext && (
            <button
              onClick={onSelectGame}
              className="hidden sm:flex items-center gap-1.5 px-3 py-1.5 bg-muted rounded-lg hover:bg-muted/80 transition-colors flex-shrink-0"
            >
              <span className="text-sm">{gameContext.emoji}</span>
              <span className="font-body text-xs text-muted-foreground">
                {gameContext.name}
              </span>
              <ChevronDown className="w-3 h-3 text-muted-foreground" />
            </button>
          )}

          {/* Input Field */}
          <div className="flex-1 relative">
            <Input
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Chiedi una regola..."
              className="pr-24 h-11 bg-card border-border font-body text-sm rounded-xl"
            />
            <div className="absolute right-2 top-1/2 -translate-y-1/2 flex items-center gap-1">
              <Button
                variant="ghost"
                size="icon"
                className="w-8 h-8 text-muted-foreground hover:text-foreground"
              >
                <Paperclip className="w-4 h-4" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="w-8 h-8 text-muted-foreground hover:text-foreground"
              >
                <Mic className="w-4 h-4" />
              </Button>
              <Button
                size="icon"
                className="w-8 h-8 bg-entity-game hover:bg-entity-game/90"
                onClick={handleSend}
                disabled={!inputValue.trim()}
              >
                <Send className="w-4 h-4" />
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

// Simple markdown formatter
function formatMarkdown(text: string): string {
  return text
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.*?)\*/g, '<em>$1</em>')
    .replace(/^### (.*$)/gim, '<h3 class="font-heading font-bold text-base mt-3 mb-1">$1</h3>')
    .replace(/^## (.*$)/gim, '<h2 class="font-heading font-bold text-lg mt-3 mb-1">$1</h2>')
    .replace(/^# (.*$)/gim, '<h1 class="font-heading font-bold text-xl mt-3 mb-1">$1</h1>')
    .replace(/^\- (.*$)/gim, '<li class="ml-4">$1</li>')
    .replace(/^\d+\. (.*$)/gim, '<li class="ml-4 list-decimal">$1</li>')
    .replace(/\n/g, '<br />')
}
