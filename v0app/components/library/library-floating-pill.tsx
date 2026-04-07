"use client"

import {
  Plus,
  Search,
  ArrowUpDown,
  LayoutGrid,
  BookOpen,
  MoreHorizontal,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"

interface LibraryFloatingPillProps {
  onAddGame?: () => void
  onFilter?: () => void
  onSort?: () => void
  onToggleView?: () => void
}

export function LibraryFloatingPill({
  onAddGame,
  onFilter,
  onSort,
  onToggleView,
}: LibraryFloatingPillProps) {
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
            <BookOpen className="w-4 h-4 text-entity-game" />
            <span className="text-muted-foreground font-body font-semibold text-[11px]">
              La mia libreria
            </span>
          </div>

          {/* Divider */}
          <div className="w-px h-6 bg-white/10" />

          {/* Actions */}
          <div className="flex items-center gap-1">
            <Button
              size="sm"
              onClick={onAddGame}
              className={cn(
                "h-8 px-4 rounded-full",
                "bg-entity-game hover:bg-entity-game/90 text-white",
                "font-body font-bold text-xs"
              )}
            >
              <Plus className="w-3.5 h-3.5 mr-1.5" />
              Aggiungi gioco
            </Button>
            <Button
              variant="ghost"
              size="icon"
              onClick={onFilter}
              className="w-8 h-8 rounded-full text-muted-foreground hover:text-foreground hover:bg-white/5"
            >
              <Search className="w-4 h-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              onClick={onSort}
              className="w-8 h-8 rounded-full text-muted-foreground hover:text-foreground hover:bg-white/5"
            >
              <ArrowUpDown className="w-4 h-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              onClick={onToggleView}
              className="w-8 h-8 rounded-full text-muted-foreground hover:text-foreground hover:bg-white/5"
            >
              <LayoutGrid className="w-4 h-4" />
            </Button>
          </div>
        </div>
      </div>

      {/* Mobile bottom action bar */}
      <div className="sm:hidden">
        {/* Bottom action bar */}
        <div className="fixed bottom-0 left-0 right-0 z-50 h-16 bg-card border-t border-border/50 px-4 pb-safe">
          <div className="h-full flex items-center justify-around">
            <button
              onClick={onAddGame}
              className="flex flex-col items-center gap-1 text-muted-foreground hover:text-foreground transition-colors"
            >
              <Plus className="w-5 h-5" />
              <span className="text-[10px] font-body font-semibold">
                Aggiungi
              </span>
            </button>
            <button
              onClick={onFilter}
              className="flex flex-col items-center gap-1 text-muted-foreground hover:text-foreground transition-colors"
            >
              <Search className="w-5 h-5" />
              <span className="text-[10px] font-body font-semibold">Cerca</span>
            </button>
            <button
              onClick={onSort}
              className="flex flex-col items-center gap-1 text-muted-foreground hover:text-foreground transition-colors"
            >
              <ArrowUpDown className="w-5 h-5" />
              <span className="text-[10px] font-body font-semibold">
                Ordina
              </span>
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
            onClick={onAddGame}
            className={cn(
              "w-12 h-12 rounded-full",
              "bg-entity-game hover:bg-entity-game/90 text-white",
              "shadow-lg shadow-entity-game/30"
            )}
          >
            <Plus className="w-5 h-5" />
          </Button>
        </div>
      </div>
    </>
  )
}
