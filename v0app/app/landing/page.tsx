import { Bot, Dices, BookOpen, ArrowRight, ChevronRight } from "lucide-react"
import { Button } from "@/components/ui/button"
import { LandingNavbar } from "@/components/landing/landing-navbar"
import { LandingFooter } from "@/components/landing/landing-footer"
import { CompactMeepleCard } from "@/components/landing/compact-meeple-card"

const features = [
  {
    icon: Bot,
    title: "Regole sempre a portata di mano",
    description: "Carica il PDF del regolamento, chiedi all'AI durante la partita e ottieni risposte immediate.",
    bgClass: "bg-entity-agent/15",
    iconClass: "text-entity-agent",
  },
  {
    icon: Dices,
    title: "Traccia ogni partita",
    description: "Punteggi, statistiche, storico sessioni. Tutto in un unico posto per ogni gioco della tua collezione.",
    bgClass: "bg-entity-game/15",
    iconClass: "text-entity-game",
  },
  {
    icon: BookOpen,
    title: "Catalogo community",
    description: "Migliaia di giochi da tavolo. Aggiungi i tuoi preferiti alla libreria e scopri nuovi titoli.",
    bgClass: "bg-entity-document/15",
    iconClass: "text-entity-document",
  },
]

const steps = [
  {
    number: "1",
    title: "Aggiungi giochi dal catalogo",
    description: "Cerca nel nostro database di migliaia di giochi o scannerizza il codice a barre.",
  },
  {
    number: "2",
    title: "Carica il regolamento PDF",
    description: "L'AI indicizza il documento per rispondere alle tue domande durante la partita.",
  },
  {
    number: "3",
    title: "Inizia una sessione e chiedi all'AI",
    description: "Traccia i punteggi e chiedi chiarimenti sulle regole in tempo reale.",
  },
  {
    number: "4",
    title: "Visualizza statistiche e storico",
    description: "Analizza le tue partite, scopri chi vince di più e migliora le tue strategie.",
  },
]

const catalogGames = [
  {
    title: "Catan",
    publisher: "Giochi Uniti",
    coverUrl: "https://images.unsplash.com/photo-1632501641765-e568d28b0015?w=400&h=300&fit=crop",
    rating: 8.2,
    playerCount: "3-4",
    duration: "90m",
  },
  {
    title: "Ticket to Ride",
    publisher: "Days of Wonder",
    coverUrl: "https://images.unsplash.com/photo-1606503153255-59d8b8b82176?w=400&h=300&fit=crop",
    rating: 7.9,
    playerCount: "2-5",
    duration: "60m",
  },
  {
    title: "Azul",
    publisher: "Next Move Games",
    coverUrl: "https://images.unsplash.com/photo-1611371805429-8b5c1b2c34ba?w=400&h=300&fit=crop",
    rating: 8.4,
    playerCount: "2-4",
    duration: "45m",
  },
  {
    title: "Wingspan",
    publisher: "Stonemaier Games",
    coverUrl: "https://images.unsplash.com/photo-1610890716171-6b1bb98ffd09?w=400&h=300&fit=crop",
    rating: 8.5,
    playerCount: "1-5",
    duration: "70m",
  },
  {
    title: "Everdell",
    publisher: "Starling Games",
    coverUrl: "https://images.unsplash.com/photo-1585504198199-20277593b94f?w=400&h=300&fit=crop",
    rating: 8.3,
    playerCount: "1-4",
    duration: "80m",
  },
  {
    title: "7 Wonders",
    publisher: "Repos Production",
    coverUrl: "https://images.unsplash.com/photo-1566694271453-390536dd7c1e?w=400&h=300&fit=crop",
    rating: 8.1,
    playerCount: "2-7",
    duration: "30m",
  },
]

export default function LandingPage() {
  return (
    <div className="min-h-screen bg-background">
      <LandingNavbar />

      {/* Hero Section */}
      <section className="relative overflow-hidden">
        {/* Background gradient */}
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_center,_hsl(25,95%,15%)_0%,_transparent_60%)] opacity-40" />
        
        <div className="relative max-w-[1200px] mx-auto px-4 py-20 md:py-32">
          <div className="flex flex-col items-center text-center">
            {/* Headline */}
            <h1 className="font-heading font-bold text-4xl md:text-5xl lg:text-[48px] text-foreground leading-tight max-w-3xl text-balance">
              Il tuo assistente AI per i giochi da tavolo
            </h1>

            {/* Subheadline */}
            <p className="font-body text-lg md:text-xl text-muted-foreground mt-6 max-w-2xl text-pretty">
              Tieni traccia delle tue partite, consulta i regolamenti con l&apos;AI e scopri nuovi giochi con la community.
            </p>

            {/* CTA Buttons */}
            <div className="flex flex-col sm:flex-row items-center gap-4 mt-10">
              <Button
                size="lg"
                className="font-body font-bold text-base bg-entity-game hover:bg-entity-game/90 text-white px-8 h-12"
              >
                Inizia gratis
              </Button>
              <Button
                variant="ghost"
                size="lg"
                className="font-body font-semibold text-base text-muted-foreground hover:text-foreground px-8 h-12"
              >
                Scopri il catalogo
                <ArrowRight className="w-4 h-4 ml-2" />
              </Button>
            </div>

            {/* Hero Visual - Stylized meeple mascot area */}
            <div className="mt-16 w-full max-w-2xl">
              <div className="relative aspect-[16/9] rounded-[16px] bg-gradient-to-br from-card to-muted border border-border/50 shadow-warm-lg overflow-hidden">
                {/* Decorative elements */}
                <div className="absolute inset-0 flex items-center justify-center">
                  <div className="relative">
                    {/* Large meeple icon */}
                    <div className="w-32 h-32 md:w-40 md:h-40 rounded-full bg-gradient-to-br from-entity-game/20 to-entity-player/20 flex items-center justify-center">
                      <svg viewBox="0 0 100 100" className="w-20 h-20 md:w-24 md:h-24">
                        <defs>
                          <linearGradient id="meepleGradient" x1="0%" y1="0%" x2="100%" y2="100%">
                            <stop offset="0%" stopColor="hsl(25, 95%, 45%)" />
                            <stop offset="100%" stopColor="hsl(262, 83%, 58%)" />
                          </linearGradient>
                        </defs>
                        {/* Meeple shape */}
                        <path
                          d="M50 10 C55 10 60 15 60 22 C60 28 56 32 50 32 C44 32 40 28 40 22 C40 15 45 10 50 10 M30 90 L35 50 L20 50 C18 50 16 48 18 46 L45 35 C48 34 52 34 55 35 L82 46 C84 48 82 50 80 50 L65 50 L70 90 L55 90 L52 55 L48 55 L45 90 Z"
                          fill="url(#meepleGradient)"
                        />
                      </svg>
                    </div>
                    {/* Floating icons */}
                    <div className="absolute -top-4 -right-8 w-12 h-12 rounded-xl bg-entity-agent/20 flex items-center justify-center animate-pulse">
                      <Bot className="w-6 h-6 text-entity-agent" />
                    </div>
                    <div className="absolute -bottom-2 -left-10 w-10 h-10 rounded-lg bg-entity-game/20 flex items-center justify-center">
                      <Dices className="w-5 h-5 text-entity-game" />
                    </div>
                    <div className="absolute top-8 -left-6 w-8 h-8 rounded-md bg-entity-document/20 flex items-center justify-center">
                      <BookOpen className="w-4 h-4 text-entity-document" />
                    </div>
                  </div>
                </div>
                {/* Dashboard preview hint */}
                <div className="absolute bottom-0 left-0 right-0 h-20 bg-gradient-to-t from-card to-transparent" />
                <div className="absolute bottom-4 left-4 right-4 flex gap-2">
                  <div className="h-2 rounded-full bg-entity-game/40 w-16" />
                  <div className="h-2 rounded-full bg-muted w-24" />
                  <div className="h-2 rounded-full bg-muted w-12" />
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* Features Section */}
      <section className="py-20 md:py-28">
        <div className="max-w-[1200px] mx-auto px-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {features.map((feature) => (
              <div
                key={feature.title}
                className="bg-card rounded-[12px] p-6 border border-border/50 shadow-warm hover:shadow-warm-lg transition-shadow"
              >
                {/* Icon */}
                <div className={`w-14 h-14 rounded-2xl ${feature.bgClass} flex items-center justify-center mb-4`}>
                  <feature.icon className={`w-7 h-7 ${feature.iconClass}`} />
                </div>
                {/* Title */}
                <h3 className="font-heading font-bold text-lg text-foreground">
                  {feature.title}
                </h3>
                {/* Description */}
                <p className="font-body text-sm text-muted-foreground mt-2 leading-relaxed">
                  {feature.description}
                </p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* How It Works Section */}
      <section id="come-funziona" className="py-20 md:py-28 bg-card/30">
        <div className="max-w-[1200px] mx-auto px-4">
          <div className="text-center mb-16">
            <h2 className="font-heading font-bold text-3xl md:text-4xl text-foreground">
              Come funziona
            </h2>
            <p className="font-body text-muted-foreground mt-4 max-w-xl mx-auto">
              Inizia a usare MeepleAi in pochi semplici passaggi
            </p>
          </div>

          <div className="relative max-w-3xl mx-auto">
            {/* Connecting line */}
            <div className="absolute left-6 md:left-1/2 top-0 bottom-0 w-0.5 bg-gradient-to-b from-entity-game via-entity-player to-entity-session -translate-x-1/2 hidden md:block" />

            <div className="flex flex-col gap-12">
              {steps.map((step, index) => (
                <div
                  key={step.number}
                  className={`flex items-start gap-6 ${index % 2 === 1 ? "md:flex-row-reverse" : ""}`}
                >
                  {/* Step number */}
                  <div className="flex-shrink-0 w-12 h-12 rounded-full bg-entity-game text-white flex items-center justify-center font-heading font-bold text-xl shadow-warm z-10">
                    {step.number}
                  </div>
                  {/* Content */}
                  <div className={`flex-1 ${index % 2 === 1 ? "md:text-right" : ""}`}>
                    <h3 className="font-heading font-bold text-lg text-foreground">
                      {step.title}
                    </h3>
                    <p className="font-body text-sm text-muted-foreground mt-2 leading-relaxed">
                      {step.description}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* Catalog Preview Section */}
      <section id="catalogo" className="py-20 md:py-28">
        <div className="max-w-[1200px] mx-auto px-4">
          <div className="flex items-center justify-between mb-8">
            <div>
              <h2 className="font-heading font-bold text-2xl md:text-3xl text-foreground">
                Scopri il catalogo community
              </h2>
              <p className="font-body text-muted-foreground mt-2">
                Migliaia di giochi da tavolo ti aspettano
              </p>
            </div>
            <a
              href="/catalogo"
              className="hidden md:flex items-center gap-2 font-body font-semibold text-sm text-entity-game hover:text-entity-game/80 transition-colors"
            >
              Sfoglia tutti i giochi
              <ChevronRight className="w-4 h-4" />
            </a>
          </div>

          {/* Horizontal scroll */}
          <div className="relative -mx-4 px-4">
            <div className="flex gap-4 overflow-x-auto pb-4 scrollbar-hide snap-x snap-mandatory">
              {catalogGames.map((game) => (
                <div key={game.title} className="snap-start">
                  <CompactMeepleCard {...game} />
                </div>
              ))}
            </div>
          </div>

          {/* Mobile CTA */}
          <div className="mt-6 md:hidden text-center">
            <a
              href="/catalogo"
              className="inline-flex items-center gap-2 font-body font-semibold text-sm text-entity-game"
            >
              Sfoglia tutti i giochi
              <ChevronRight className="w-4 h-4" />
            </a>
          </div>
        </div>
      </section>

      {/* CTA Section */}
      <section className="py-20 md:py-28">
        <div className="max-w-[800px] mx-auto px-4">
          <div className="bg-card rounded-[16px] p-8 md:p-12 text-center border border-border/50 shadow-warm-lg">
            <h2 className="font-heading font-bold text-2xl md:text-[32px] text-foreground">
              Pronto a giocare meglio?
            </h2>
            <p className="font-body text-muted-foreground mt-4 max-w-md mx-auto">
              Unisciti a migliaia di giocatori che usano MeepleAi per migliorare le loro serate di gioco.
            </p>
            <Button
              size="lg"
              className="mt-8 font-body font-bold text-base bg-entity-game hover:bg-entity-game/90 text-white px-10 h-12"
            >
              Registrati gratis
            </Button>
          </div>
        </div>
      </section>

      <LandingFooter />
    </div>
  )
}
