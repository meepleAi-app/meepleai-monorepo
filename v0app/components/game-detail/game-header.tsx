import { Badge } from "@/components/ui/badge"
import { Users, Clock, Star } from "lucide-react"

interface GameHeaderProps {
  title: string
  players: string
  duration: string
  rating: string
}

export function GameHeader({ title, players, duration, rating }: GameHeaderProps) {
  return (
    <section className="flex flex-col sm:flex-row items-start gap-5">
      {/* Game cover image */}
      <div className="relative flex-shrink-0">
        {/* Entity badge */}
        <div className="absolute -top-2 -left-2 z-10">
          <Badge 
            className="bg-entity-game/90 text-white border-0 font-heading font-bold text-[10px] uppercase px-2 py-0.5 tracking-wide"
          >
            Gioco
          </Badge>
        </div>
        {/* Cover with orange border accent */}
        <div className="w-20 h-20 rounded-xl overflow-hidden border-2 border-entity-game/50 shadow-warm">
          <div className="w-full h-full bg-gradient-to-br from-entity-game/40 via-amber-500/30 to-orange-600/40" />
        </div>
      </div>

      {/* Game info */}
      <div className="flex-1 min-w-0">
        <h1 className="font-heading font-bold text-2xl text-foreground mb-3 text-balance">
          {title}
        </h1>
        
        {/* Metadata pills */}
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="secondary" className="bg-muted/80 text-muted-foreground border-0 font-body font-medium text-xs px-2.5 py-1">
            <Users className="w-3 h-3 mr-1.5" />
            {players}
          </Badge>
          <Badge variant="secondary" className="bg-muted/80 text-muted-foreground border-0 font-body font-medium text-xs px-2.5 py-1">
            <Clock className="w-3 h-3 mr-1.5" />
            {duration}
          </Badge>
          <Badge variant="secondary" className="bg-muted/80 text-muted-foreground border-0 font-body font-medium text-xs px-2.5 py-1">
            <Star className="w-3 h-3 mr-1.5 fill-amber-400 text-amber-400" />
            {rating} BGG
          </Badge>
        </div>
      </div>
    </section>
  )
}
