"use client"

import { useState } from "react"
import { Plus, Minus, UserPlus } from "lucide-react"
import { cn } from "@/lib/utils"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"

interface Player {
  id: string
  name: string
  initials: string
  score: number
  color: string
}

interface ScoreTrackerProps {
  gameName: string
  gameCover: string
  duration: string
  players: Player[]
  onScoreChange: (playerId: string, delta: number) => void
  onAddPlayer: () => void
}

export function ScoreTracker({
  gameName,
  gameCover,
  duration,
  players,
  onScoreChange,
  onAddPlayer,
}: ScoreTrackerProps) {
  // Sort players by score to determine rankings
  const sortedPlayers = [...players].sort((a, b) => b.score - a.score)
  const rankings = new Map(sortedPlayers.map((p, i) => [p.id, i + 1]))

  const getRankEmoji = (rank: number) => {
    switch (rank) {
      case 1:
        return "🥇"
      case 2:
        return "🥈"
      case 3:
        return "🥉"
      default:
        return null
    }
  }

  return (
    <div className="flex-1 flex flex-col gap-6">
      {/* Game Context Header */}
      <div className="flex items-center gap-4 p-4 bg-card rounded-xl border border-border">
        <div className="w-14 h-14 rounded-lg overflow-hidden border-2 border-entity-game/50 flex-shrink-0">
          <img
            src={gameCover}
            alt={gameName}
            className="w-full h-full object-cover"
          />
        </div>
        <div className="flex-1 min-w-0">
          <h2 className="font-heading font-bold text-lg text-foreground truncate">
            {gameName}
          </h2>
          <p className="text-sm text-muted-foreground font-body">
            Sessione in corso
          </p>
        </div>
        <div className="text-right">
          <p className="font-heading font-bold text-xl text-entity-game tabular-nums">
            {duration}
          </p>
          <p className="text-xs text-muted-foreground font-body">durata</p>
        </div>
      </div>

      {/* Players Score List */}
      <div className="flex flex-col gap-3">
        {players.map((player) => {
          const rank = rankings.get(player.id) || 0
          const isWinner = rank === 1 && players.length > 1
          const rankEmoji = getRankEmoji(rank)

          return (
            <div
              key={player.id}
              className={cn(
                "relative flex items-center gap-4 p-4 bg-card rounded-xl border transition-all",
                isWinner
                  ? "border-entity-game/50 shadow-[0_0_20px_rgba(180,130,80,0.15)]"
                  : "border-border hover:border-border/80"
              )}
            >
              {/* Winner left accent */}
              {isWinner && (
                <div className="absolute left-0 top-3 bottom-3 w-1 bg-entity-game rounded-full" />
              )}

              {/* Avatar */}
              <Avatar className="w-10 h-10 border-2" style={{ borderColor: player.color }}>
                <AvatarFallback
                  className="text-white text-sm font-body font-bold"
                  style={{ background: `linear-gradient(135deg, ${player.color}, ${player.color}dd)` }}
                >
                  {player.initials}
                </AvatarFallback>
              </Avatar>

              {/* Name */}
              <div className="flex-1 min-w-0">
                <p className="font-body font-bold text-sm text-foreground truncate">
                  {player.name}
                </p>
              </div>

              {/* Score controls */}
              <div className="flex items-center gap-3">
                <Button
                  variant="secondary"
                  size="icon"
                  className="w-8 h-8 rounded-full"
                  onClick={() => onScoreChange(player.id, -1)}
                >
                  <Minus className="w-4 h-4" />
                </Button>

                <span className="font-heading font-bold text-2xl text-foreground w-12 text-center tabular-nums">
                  {player.score}
                </span>

                <Button
                  variant="secondary"
                  size="icon"
                  className="w-8 h-8 rounded-full"
                  onClick={() => onScoreChange(player.id, 1)}
                >
                  <Plus className="w-4 h-4" />
                </Button>
              </div>

              {/* Rank indicator */}
              {rankEmoji && (
                <span className="text-xl w-8 text-center">{rankEmoji}</span>
              )}
            </div>
          )
        })}

        {/* Add player button */}
        <button
          onClick={onAddPlayer}
          className="flex items-center justify-center gap-2 p-4 rounded-xl border-2 border-dashed border-border hover:border-muted-foreground/50 transition-colors text-muted-foreground hover:text-foreground"
        >
          <UserPlus className="w-5 h-5" />
          <span className="font-body font-semibold text-sm">Aggiungi giocatore</span>
        </button>
      </div>
    </div>
  )
}
