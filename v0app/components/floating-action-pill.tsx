"use client"

import { Play, Bot, Heart, Share2, MoreHorizontal, Gamepad2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"

interface FloatingActionPillProps {
  gameName: string
}

export function FloatingActionPill({ gameName }: FloatingActionPillProps) {
  return (
    <>
      {/* Desktop floating pill */}
      <div className="hidden sm:flex fixed bottom-6 left-1/2 -translate-x-1/2 z-50">
        <div
          className={cn(
            "flex items-center gap-2 px-3 py-2",
            "bg-[rgba(30,41,59,0.85)] backdrop-blur-md",
            "border border-white/10 rounded-[40px]",
            "shadow-[0_8px_32px_rgba(0,0,0,0.4)]"
          )}
        >
          {/* Context label */}
          <div className="flex items-center gap-2 px-2">
            <Gamepad2 className="w-4 h-4 text-entity-game" />
            <span className="text-muted-foreground font-body font-semibold text-[11px]">
              {gameName}
            </span>
          </div>

          {/* Divider */}
          <div className="w-px h-6 bg-white/10" />

          {/* Actions */}
          <div className="flex items-center gap-1">
            <Button
              size="sm"
              className={cn(
                "h-8 px-4 rounded-full",
                "bg-entity-game hover:bg-entity-game/90 text-white",
                "font-body font-bold text-xs"
              )}
            >
              <Play className="w-3.5 h-3.5 mr-1.5" />
              Nuova sessione
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="w-8 h-8 rounded-full text-muted-foreground hover:text-foreground hover:bg-white/5"
            >
              <Bot className="w-4 h-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="w-8 h-8 rounded-full text-muted-foreground hover:text-foreground hover:bg-white/5"
            >
              <Heart className="w-4 h-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="w-8 h-8 rounded-full text-muted-foreground hover:text-foreground hover:bg-white/5"
            >
              <Share2 className="w-4 h-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="w-8 h-8 rounded-full text-muted-foreground hover:text-foreground hover:bg-white/5"
            >
              <MoreHorizontal className="w-4 h-4" />
            </Button>
          </div>
        </div>
      </div>

      {/* Mobile: Context breadcrumb + bottom action bar */}
      <div className="sm:hidden">
        {/* Context breadcrumb strip */}
        <div className="fixed top-[92px] left-0 right-0 z-40 h-10 bg-card/95 backdrop-blur-sm border-b border-border/50 px-4 flex items-center">
          <div className="flex items-center gap-2">
            <Gamepad2 className="w-4 h-4 text-entity-game" />
            <span className="text-foreground font-body font-semibold text-sm truncate">
              {gameName}
            </span>
          </div>
        </div>

        {/* Bottom action bar */}
        <div className="fixed bottom-0 left-0 right-0 z-50 h-16 bg-card border-t border-border/50 px-4 pb-safe">
          <div className="h-full flex items-center justify-around">
            <button className="flex flex-col items-center gap-1 text-muted-foreground hover:text-foreground transition-colors">
              <Bot className="w-5 h-5" />
              <span className="text-[10px] font-body font-semibold">AI</span>
            </button>
            <button className="flex flex-col items-center gap-1 text-muted-foreground hover:text-foreground transition-colors">
              <Heart className="w-5 h-5" />
              <span className="text-[10px] font-body font-semibold">Preferiti</span>
            </button>
            <button className="flex flex-col items-center gap-1 text-muted-foreground hover:text-foreground transition-colors">
              <Share2 className="w-5 h-5" />
              <span className="text-[10px] font-body font-semibold">Condividi</span>
            </button>
            <button className="flex flex-col items-center gap-1 text-muted-foreground hover:text-foreground transition-colors">
              <MoreHorizontal className="w-5 h-5" />
              <span className="text-[10px] font-body font-semibold">Altro</span>
            </button>
          </div>
        </div>

        {/* FAB */}
        <div className="fixed bottom-20 right-4 z-50">
          <Button
            size="icon"
            className={cn(
              "w-12 h-12 rounded-full",
              "bg-entity-game hover:bg-entity-game/90 text-white",
              "shadow-lg shadow-entity-game/30"
            )}
          >
            <Play className="w-5 h-5" />
          </Button>
        </div>
      </div>
    </>
  )
}
