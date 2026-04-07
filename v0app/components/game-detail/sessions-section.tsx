import { Card } from "@/components/ui/card"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Calendar, Clock, Trophy } from "lucide-react"
import { cn } from "@/lib/utils"

interface Player {
  name: string
  initials: string
  avatar?: string
  isWinner?: boolean
}

interface Session {
  date: string
  duration: string
  players: Player[]
  winner: string
}

const sessions: Session[] = [
  {
    date: "28 Mar 2024",
    duration: "2h 15m",
    players: [
      { name: "Mario", initials: "MR", isWinner: true },
      { name: "Giulia", initials: "GI" },
      { name: "Luca", initials: "LU" },
      { name: "Sara", initials: "SA" },
    ],
    winner: "Mario",
  },
  {
    date: "21 Mar 2024",
    duration: "1h 45m",
    players: [
      { name: "Giulia", initials: "GI", isWinner: true },
      { name: "Mario", initials: "MR" },
      { name: "Paolo", initials: "PA" },
    ],
    winner: "Giulia",
  },
  {
    date: "14 Mar 2024",
    duration: "2h 30m",
    players: [
      { name: "Luca", initials: "LU", isWinner: true },
      { name: "Sara", initials: "SA" },
      { name: "Mario", initials: "MR" },
      { name: "Anna", initials: "AN" },
    ],
    winner: "Luca",
  },
]

const playerColors = [
  "bg-entity-player",
  "bg-entity-chat",
  "bg-entity-agent",
  "bg-entity-event",
]

function SessionCard({ session, index }: { session: Session; index: number }) {
  return (
    <Card
      className={cn(
        "bg-card border-border/50 p-4",
        "shadow-warm hover:bg-card-hover transition-colors"
      )}
    >
      {/* Date and duration */}
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2 text-muted-foreground">
          <Calendar className="w-3.5 h-3.5" />
          <span className="font-body font-medium text-xs">{session.date}</span>
        </div>
        <div className="flex items-center gap-1.5 text-muted-foreground">
          <Clock className="w-3.5 h-3.5" />
          <span className="font-body font-medium text-xs">{session.duration}</span>
        </div>
      </div>

      {/* Players avatars */}
      <div className="flex items-center mb-3">
        <div className="flex -space-x-2">
          {session.players.map((player, pIndex) => (
            <Avatar
              key={pIndex}
              className={cn(
                "w-7 h-7 border-2 border-card",
                player.isWinner && "ring-2 ring-entity-agent ring-offset-1 ring-offset-card"
              )}
            >
              <AvatarImage src={player.avatar} alt={player.name} />
              <AvatarFallback
                className={cn(
                  "text-white text-[10px] font-body font-bold",
                  playerColors[pIndex % playerColors.length]
                )}
              >
                {player.initials}
              </AvatarFallback>
            </Avatar>
          ))}
        </div>
        <span className="ml-2 font-body text-xs text-muted-foreground">
          {session.players.length} giocatori
        </span>
      </div>

      {/* Winner */}
      <div className="flex items-center gap-2">
        <Badge
          variant="secondary"
          className="bg-entity-agent/15 text-entity-agent border-0 font-body font-semibold text-[10px] px-2 py-0.5"
        >
          <Trophy className="w-3 h-3 mr-1" />
          Vincitore
        </Badge>
        <span className="font-body font-semibold text-sm text-foreground">
          {session.winner}
        </span>
      </div>
    </Card>
  )
}

export function SessionsSection() {
  return (
    <section>
      <div className="flex items-center justify-between mb-4">
        <h2 className="font-heading font-bold text-lg text-foreground">
          Ultime sessioni
        </h2>
        <a
          href="#"
          className="font-body font-semibold text-xs text-entity-game hover:text-entity-game/80 transition-colors"
        >
          Vedi tutte
        </a>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {sessions.map((session, index) => (
          <SessionCard key={index} session={session} index={index} />
        ))}
      </div>
    </section>
  )
}
