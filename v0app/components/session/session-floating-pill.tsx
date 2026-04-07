"use client"

import { Play, Pause, Bot, CheckCircle, StickyNote, Timer } from "lucide-react"
import { Button } from "@/components/ui/button"

interface SessionFloatingPillProps {
  duration: string
  isPaused: boolean
  onTogglePause: () => void
  onEndSession: () => void
  onOpenAI: () => void
  onOpenTimer: () => void
  onOpenNotes: () => void
}

export function SessionFloatingPill({
  duration,
  isPaused,
  onTogglePause,
  onEndSession,
  onOpenAI,
  onOpenTimer,
  onOpenNotes,
}: SessionFloatingPillProps) {
  return (
    <>
      {/* Desktop Floating Pill */}
      <div className="hidden lg:flex fixed bottom-6 left-1/2 -translate-x-1/2 z-50">
        <div className="flex items-center gap-3 px-5 py-3 bg-card/90 backdrop-blur-md border border-border rounded-full shadow-warm-lg">
          {/* Context */}
          <div className="flex items-center gap-2 pr-3 border-r border-border">
            <Play className="w-4 h-4 text-entity-game" />
            <span className="font-body font-semibold text-sm text-foreground">
              Sessione in corso
            </span>
            <span className="font-heading font-bold text-sm text-entity-game tabular-nums">
              · {duration}
            </span>
          </div>

          {/* Actions */}
          <div className="flex items-center gap-2">
            <Button
              size="sm"
              className="bg-entity-agent hover:bg-entity-agent/90 text-white font-body font-semibold"
              onClick={onOpenAI}
            >
              <Bot className="w-4 h-4 mr-1.5" />
              AI
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="font-body font-semibold text-muted-foreground hover:text-foreground"
              onClick={onTogglePause}
            >
              {isPaused ? (
                <>
                  <Play className="w-4 h-4 mr-1.5" />
                  Riprendi
                </>
              ) : (
                <>
                  <Pause className="w-4 h-4 mr-1.5" />
                  Pausa
                </>
              )}
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="font-body font-semibold text-destructive hover:text-destructive hover:bg-destructive/10"
              onClick={onEndSession}
            >
              <CheckCircle className="w-4 h-4 mr-1.5" />
              Termina
            </Button>
          </div>
        </div>
      </div>

      {/* Mobile Bottom Action Bar */}
      <div className="lg:hidden fixed bottom-0 left-0 right-0 z-50 bg-card border-t border-border">
        <div className="flex items-center justify-around py-2 px-4">
          <button
            onClick={onOpenAI}
            className="flex flex-col items-center gap-1 py-2 px-3 text-muted-foreground hover:text-entity-agent transition-colors"
          >
            <Bot className="w-5 h-5" />
            <span className="text-xs font-body font-semibold">AI</span>
          </button>
          <button
            onClick={onOpenTimer}
            className="flex flex-col items-center gap-1 py-2 px-3 text-muted-foreground hover:text-foreground transition-colors"
          >
            <Timer className="w-5 h-5" />
            <span className="text-xs font-body font-semibold">Timer</span>
          </button>
          <button
            onClick={onOpenNotes}
            className="flex flex-col items-center gap-1 py-2 px-3 text-muted-foreground hover:text-foreground transition-colors"
          >
            <StickyNote className="w-5 h-5" />
            <span className="text-xs font-body font-semibold">Note</span>
          </button>
          <button
            onClick={onTogglePause}
            className="flex flex-col items-center gap-1 py-2 px-3 text-muted-foreground hover:text-foreground transition-colors"
          >
            {isPaused ? <Play className="w-5 h-5" /> : <Pause className="w-5 h-5" />}
            <span className="text-xs font-body font-semibold">
              {isPaused ? "Riprendi" : "Pausa"}
            </span>
          </button>
          <button
            onClick={onEndSession}
            className="flex flex-col items-center gap-1 py-2 px-3 text-destructive hover:text-destructive/80 transition-colors"
          >
            <CheckCircle className="w-5 h-5" />
            <span className="text-xs font-body font-semibold">Termina</span>
          </button>
        </div>
      </div>

      {/* Mobile FAB for AI */}
      <div className="lg:hidden fixed bottom-20 right-4 z-50">
        <Button
          size="icon"
          className="w-14 h-14 rounded-full bg-entity-game hover:bg-entity-game/90 shadow-warm-lg"
          onClick={onOpenAI}
        >
          <Bot className="w-6 h-6" />
        </Button>
      </div>
    </>
  )
}
