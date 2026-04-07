"use client"

import { useState } from "react"
import { Star, Users, Clock, Heart } from "lucide-react"
import { cn } from "@/lib/utils"

export interface Game {
  id: string
  title: string
  publisher: string
  coverUrl: string
  rating: number
  playerCount: string
  duration: string
  lastPlayed?: string
  isWishlisted?: boolean
}

interface MeepleCardProps {
  game: Game
  onWishlistToggle?: (id: string) => void
}

export function MeepleCard({ game, onWishlistToggle }: MeepleCardProps) {
  const [wishlisted, setWishlisted] = useState(game.isWishlisted ?? false)

  const handleWishlistClick = (e: React.MouseEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setWishlisted(!wishlisted)
    onWishlistToggle?.(game.id)
  }

  return (
    <a
      href={`/giochi/${game.id}`}
      className={cn(
        "group relative block",
        "bg-card rounded-[12px] overflow-hidden",
        "border-l-4 border-l-entity-game",
        "shadow-warm transition-all duration-200",
        "hover:border-l-[6px] hover:shadow-warm-lg",
        "hover:ring-2 hover:ring-entity-game/30"
      )}
    >
      {/* Entity badge */}
      <div className="absolute top-3 left-3 z-10">
        <span className="px-2 py-0.5 bg-entity-game text-white font-heading font-bold text-[10px] uppercase rounded">
          Gioco
        </span>
      </div>

      {/* Cover image */}
      <div className="aspect-[4/3] overflow-hidden bg-muted">
        <img
          src={game.coverUrl}
          alt={game.title}
          className="w-full h-full object-cover transition-transform duration-300 group-hover:scale-105"
        />
      </div>

      {/* Card body */}
      <div className="p-3">
        {/* Title */}
        <h3 className="font-heading font-bold text-sm text-foreground leading-tight truncate">
          {game.title}
        </h3>

        {/* Publisher */}
        <p className="font-body text-xs text-muted-foreground mt-0.5 truncate">
          {game.publisher}
        </p>

        {/* Metadata row */}
        <div className="flex items-center gap-3 mt-2">
          <div className="flex items-center gap-1">
            <Star className="w-3.5 h-3.5 text-entity-agent fill-entity-agent" />
            <span className="font-body font-semibold text-[11px] text-foreground">
              {game.rating.toFixed(1)}
            </span>
          </div>
          <div className="flex items-center gap-1 text-muted-foreground">
            <Users className="w-3.5 h-3.5" />
            <span className="font-body font-semibold text-[11px]">
              {game.playerCount}
            </span>
          </div>
          <div className="flex items-center gap-1 text-muted-foreground">
            <Clock className="w-3.5 h-3.5" />
            <span className="font-body font-semibold text-[11px]">
              {game.duration}
            </span>
          </div>
        </div>

        {/* Bottom row */}
        <div className="flex items-center justify-between mt-2 pt-2 border-t border-border/50">
          {game.lastPlayed ? (
            <span className="font-body text-[11px] text-muted-foreground">
              Ultima: {game.lastPlayed}
            </span>
          ) : (
            <span className="font-body text-[11px] text-muted-foreground">
              Mai giocato
            </span>
          )}
          <button
            onClick={handleWishlistClick}
            className={cn(
              "p-1 rounded-full transition-colors",
              wishlisted
                ? "text-entity-event"
                : "text-muted-foreground hover:text-entity-event"
            )}
          >
            <Heart
              className={cn("w-4 h-4", wishlisted && "fill-entity-event")}
            />
          </button>
        </div>
      </div>
    </a>
  )
}
