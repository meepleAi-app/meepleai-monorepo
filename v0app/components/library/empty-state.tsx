import { Dices } from "lucide-react"
import { Button } from "@/components/ui/button"

export function EmptyState() {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4 text-center">
      {/* Illustration area */}
      <div className="relative mb-6">
        <div className="w-24 h-24 rounded-full bg-muted/50 flex items-center justify-center">
          <Dices className="w-12 h-12 text-muted-foreground" />
        </div>
        {/* Decorative dots */}
        <div className="absolute -top-2 -right-2 w-4 h-4 rounded-full bg-entity-game/30" />
        <div className="absolute -bottom-1 -left-3 w-3 h-3 rounded-full bg-entity-player/30" />
        <div className="absolute top-1/2 -right-4 w-2 h-2 rounded-full bg-entity-agent/30" />
      </div>

      {/* Heading */}
      <h2 className="font-heading font-bold text-lg text-foreground mb-2">
        La tua libreria è vuota
      </h2>

      {/* Subtext */}
      <p className="font-body text-sm text-muted-foreground max-w-[280px] mb-6">
        Aggiungi il tuo primo gioco dal catalogo community
      </p>

      {/* CTA */}
      <Button
        className="bg-entity-game hover:bg-entity-game/90 text-white font-body font-bold rounded-button"
        asChild
      >
        <a href="/catalogo">Sfoglia il catalogo</a>
      </Button>
    </div>
  )
}
