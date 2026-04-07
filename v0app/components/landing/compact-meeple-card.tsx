import { Star, Users, Clock } from "lucide-react"
import { cn } from "@/lib/utils"

interface CompactMeepleCardProps {
  title: string
  publisher: string
  coverUrl: string
  rating: number
  playerCount: string
  duration: string
}

export function CompactMeepleCard({
  title,
  publisher,
  coverUrl,
  rating,
  playerCount,
  duration,
}: CompactMeepleCardProps) {
  return (
    <div
      className={cn(
        "flex-shrink-0 w-[200px]",
        "bg-card rounded-[12px] overflow-hidden",
        "border-l-4 border-l-entity-game",
        "shadow-warm transition-all duration-200",
        "hover:border-l-[6px] hover:shadow-warm-lg",
        "hover:ring-2 hover:ring-entity-game/30"
      )}
    >
      {/* Cover image */}
      <div className="aspect-[4/3] overflow-hidden bg-muted">
        <img
          src={coverUrl}
          alt={title}
          className="w-full h-full object-cover"
        />
      </div>

      {/* Card body */}
      <div className="p-3">
        <h3 className="font-heading font-bold text-sm text-foreground leading-tight truncate">
          {title}
        </h3>
        <p className="font-body text-xs text-muted-foreground mt-0.5 truncate">
          {publisher}
        </p>

        {/* Metadata row */}
        <div className="flex items-center gap-3 mt-2">
          <div className="flex items-center gap-1">
            <Star className="w-3.5 h-3.5 text-entity-agent fill-entity-agent" />
            <span className="font-body font-semibold text-[11px] text-foreground">
              {rating.toFixed(1)}
            </span>
          </div>
          <div className="flex items-center gap-1 text-muted-foreground">
            <Users className="w-3.5 h-3.5" />
            <span className="font-body font-semibold text-[11px]">
              {playerCount}
            </span>
          </div>
          <div className="flex items-center gap-1 text-muted-foreground">
            <Clock className="w-3.5 h-3.5" />
            <span className="font-body font-semibold text-[11px]">
              {duration}
            </span>
          </div>
        </div>
      </div>
    </div>
  )
}
