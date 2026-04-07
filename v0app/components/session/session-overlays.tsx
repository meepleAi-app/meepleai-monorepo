"use client"

import { Play, Share2, Trophy } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"

interface Player {
  id: string
  name: string
  initials: string
  score: number
  color: string
}

interface PausedOverlayProps {
  onResume: () => void
}

export function PausedOverlay({ onResume }: PausedOverlayProps) {
  return (
    <div className="fixed inset-0 z-[60] bg-background/80 backdrop-blur-sm flex items-center justify-center">
      <div className="flex flex-col items-center gap-6 text-center">
        <div className="w-20 h-20 rounded-full bg-muted flex items-center justify-center">
          <Play className="w-10 h-10 text-entity-game ml-1" />
        </div>
        <div>
          <h2 className="font-heading font-bold text-2xl text-foreground mb-2">
            Sessione in pausa
          </h2>
          <p className="text-muted-foreground font-body">
            La partita è stata messa in pausa
          </p>
        </div>
        <Button
          size="lg"
          className="bg-entity-game hover:bg-entity-game/90 font-body font-bold px-8"
          onClick={onResume}
        >
          <Play className="w-5 h-5 mr-2" />
          Riprendi partita
        </Button>
      </div>
    </div>
  )
}

interface EndedOverlayProps {
  players: Player[]
  gameName: string
  duration: string
  onSaveAndShare: () => void
  onClose: () => void
}

export function EndedOverlay({
  players,
  gameName,
  duration,
  onSaveAndShare,
  onClose,
}: EndedOverlayProps) {
  // Sort players by score for podium
  const sortedPlayers = [...players].sort((a, b) => b.score - a.score)
  const podium = sortedPlayers.slice(0, 3)

  // Reorder for podium display: [2nd, 1st, 3rd]
  const podiumOrder = podium.length >= 2 
    ? [podium[1], podium[0], podium[2]].filter(Boolean)
    : podium

  const podiumHeights = ["h-24", "h-32", "h-16"]
  const podiumColors = ["bg-gray-400", "bg-yellow-500", "bg-amber-700"]
  const podiumLabels = ["2°", "1°", "3°"]

  return (
    <div className="fixed inset-0 z-[60] bg-background/95 backdrop-blur-sm flex items-center justify-center p-4">
      <div className="w-full max-w-md flex flex-col items-center gap-8">
        {/* Trophy Icon */}
        <div className="w-16 h-16 rounded-full bg-entity-game/20 flex items-center justify-center">
          <Trophy className="w-8 h-8 text-entity-game" />
        </div>

        {/* Title */}
        <div className="text-center">
          <h2 className="font-heading font-bold text-2xl text-foreground mb-2">
            Partita conclusa!
          </h2>
          <p className="text-muted-foreground font-body">
            {gameName} · {duration}
          </p>
        </div>

        {/* Podium */}
        <div className="flex items-end justify-center gap-4 w-full">
          {podiumOrder.map((player, index) => {
            const actualIndex = index === 1 ? 0 : index === 0 ? 1 : 2
            return (
              <div key={player.id} className="flex flex-col items-center gap-3">
                {/* Player Avatar */}
                <Avatar className="w-12 h-12 border-2" style={{ borderColor: player.color }}>
                  <AvatarFallback
                    className="text-white text-sm font-body font-bold"
                    style={{ background: `linear-gradient(135deg, ${player.color}, ${player.color}dd)` }}
                  >
                    {player.initials}
                  </AvatarFallback>
                </Avatar>
                <div className="text-center">
                  <p className="font-body font-bold text-sm text-foreground">
                    {player.name}
                  </p>
                  <p className="font-heading font-bold text-lg text-entity-game">
                    {player.score} pt
                  </p>
                </div>
                {/* Podium Block */}
                <div
                  className={`w-20 ${podiumHeights[actualIndex]} ${podiumColors[actualIndex]} rounded-t-lg flex items-start justify-center pt-3`}
                >
                  <span className="font-heading font-bold text-xl text-white">
                    {podiumLabels[actualIndex]}
                  </span>
                </div>
              </div>
            )
          })}
        </div>

        {/* Full Results */}
        {sortedPlayers.length > 3 && (
          <div className="w-full bg-card rounded-xl border border-border p-4">
            <h3 className="font-body font-semibold text-sm text-muted-foreground mb-3">
              Classifica completa
            </h3>
            <div className="flex flex-col gap-2">
              {sortedPlayers.slice(3).map((player, index) => (
                <div
                  key={player.id}
                  className="flex items-center gap-3 py-2 px-3 bg-muted/50 rounded-lg"
                >
                  <span className="font-body font-bold text-sm text-muted-foreground w-6">
                    {index + 4}°
                  </span>
                  <Avatar className="w-7 h-7 border" style={{ borderColor: player.color }}>
                    <AvatarFallback
                      className="text-white text-xs font-body font-bold"
                      style={{ background: player.color }}
                    >
                      {player.initials}
                    </AvatarFallback>
                  </Avatar>
                  <span className="flex-1 font-body font-semibold text-sm text-foreground">
                    {player.name}
                  </span>
                  <span className="font-heading font-bold text-sm text-foreground">
                    {player.score} pt
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Actions */}
        <div className="flex flex-col sm:flex-row gap-3 w-full">
          <Button
            variant="outline"
            className="flex-1 font-body font-semibold"
            onClick={onClose}
          >
            Chiudi
          </Button>
          <Button
            className="flex-1 bg-entity-game hover:bg-entity-game/90 font-body font-bold"
            onClick={onSaveAndShare}
          >
            <Share2 className="w-4 h-4 mr-2" />
            Salva e condividi
          </Button>
        </div>
      </div>
    </div>
  )
}
