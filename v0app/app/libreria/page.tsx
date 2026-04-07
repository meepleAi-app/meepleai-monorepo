"use client"

import { useState } from "react"
import { LayoutGrid, List } from "lucide-react"
import { Navbar } from "@/components/navbar"
import { MeepleCard, Game } from "@/components/library/meeple-card"
import { FilterBar } from "@/components/library/filter-bar"
import { EmptyState } from "@/components/library/empty-state"
import { LibraryFloatingPill } from "@/components/library/library-floating-pill"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { cn } from "@/lib/utils"

// Mock data for games
const mockGames: Game[] = [
  {
    id: "1",
    title: "Wingspan",
    publisher: "Stonemaier Games",
    coverUrl: "https://images.unsplash.com/photo-1610890716171-6b1bb98ffd09?w=400&h=300&fit=crop",
    rating: 8.1,
    playerCount: "1-5",
    duration: "60m",
    lastPlayed: "3 gg fa",
    isWishlisted: false,
  },
  {
    id: "2",
    title: "Terraforming Mars",
    publisher: "FryxGames",
    coverUrl: "https://images.unsplash.com/photo-1614732414444-096e5f1122d5?w=400&h=300&fit=crop",
    rating: 8.4,
    playerCount: "1-5",
    duration: "120m",
    lastPlayed: "1 sett fa",
    isWishlisted: true,
  },
  {
    id: "3",
    title: "Azul",
    publisher: "Plan B Games",
    coverUrl: "https://images.unsplash.com/photo-1606503153255-59d8b8b82176?w=400&h=300&fit=crop",
    rating: 7.8,
    playerCount: "2-4",
    duration: "45m",
    lastPlayed: "2 sett fa",
    isWishlisted: false,
  },
  {
    id: "4",
    title: "Scythe",
    publisher: "Stonemaier Games",
    coverUrl: "https://images.unsplash.com/photo-1611891487122-207579d67d98?w=400&h=300&fit=crop",
    rating: 8.3,
    playerCount: "1-5",
    duration: "115m",
    lastPlayed: "1 mese fa",
    isWishlisted: false,
  },
  {
    id: "5",
    title: "Everdell",
    publisher: "Starling Games",
    coverUrl: "https://images.unsplash.com/photo-1632501641765-e568d28b0015?w=400&h=300&fit=crop",
    rating: 8.0,
    playerCount: "1-4",
    duration: "80m",
    isWishlisted: true,
  },
  {
    id: "6",
    title: "Root",
    publisher: "Leder Games",
    coverUrl: "https://images.unsplash.com/photo-1585504198199-20277593b94f?w=400&h=300&fit=crop",
    rating: 8.1,
    playerCount: "2-4",
    duration: "90m",
    lastPlayed: "5 gg fa",
    isWishlisted: false,
  },
  {
    id: "7",
    title: "Spirit Island",
    publisher: "Greater Than Games",
    coverUrl: "https://images.unsplash.com/photo-1569003339405-ea396a5a8a90?w=400&h=300&fit=crop",
    rating: 8.4,
    playerCount: "1-4",
    duration: "120m",
    lastPlayed: "2 sett fa",
    isWishlisted: false,
  },
  {
    id: "8",
    title: "Brass: Birmingham",
    publisher: "Roxley Games",
    coverUrl: "https://images.unsplash.com/photo-1611371805429-8b5c1b2c34ba?w=400&h=300&fit=crop",
    rating: 8.6,
    playerCount: "2-4",
    duration: "120m",
    isWishlisted: true,
  },
]

export default function LibraryPage() {
  const [viewMode, setViewMode] = useState<"grid" | "list">("grid")
  const [activeFilter, setActiveFilter] = useState("Tutti")
  const [activePlayerFilter, setActivePlayerFilter] = useState<string | null>(null)
  const [sortBy, setSortBy] = useState("az")
  const [games, setGames] = useState<Game[]>(mockGames)
  
  // Toggle to show empty state for testing
  const [showEmpty] = useState(false)
  
  const displayedGames = showEmpty ? [] : games

  const handleWishlistToggle = (id: string) => {
    setGames((prev) =>
      prev.map((game) =>
        game.id === id ? { ...game, isWishlisted: !game.isWishlisted } : game
      )
    )
  }

  return (
    <div className="min-h-screen bg-background pb-24 sm:pb-20">
      <Navbar activeLink="Libreria" />

      <main className="max-w-[1200px] mx-auto px-4 py-6">
        {/* Page header */}
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <h1 className="font-heading font-bold text-[22px] text-foreground">
              La mia libreria
            </h1>
            <Badge
              variant="secondary"
              className="font-body font-semibold text-xs"
            >
              {displayedGames.length} giochi
            </Badge>
          </div>

          {/* View toggle (desktop) */}
          <div className="hidden sm:flex items-center gap-1 bg-muted rounded-lg p-1">
            <Button
              variant="ghost"
              size="icon"
              className={cn(
                "w-8 h-8 rounded-md",
                viewMode === "grid" && "bg-card shadow-sm"
              )}
              onClick={() => setViewMode("grid")}
            >
              <LayoutGrid className="w-4 h-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className={cn(
                "w-8 h-8 rounded-md",
                viewMode === "list" && "bg-card shadow-sm"
              )}
              onClick={() => setViewMode("list")}
            >
              <List className="w-4 h-4" />
            </Button>
          </div>
        </div>

        {/* Filter bar */}
        <div className="mb-6">
          <FilterBar
            activeFilter={activeFilter}
            onFilterChange={setActiveFilter}
            activePlayerFilter={activePlayerFilter}
            onPlayerFilterChange={setActivePlayerFilter}
            sortBy={sortBy}
            onSortChange={setSortBy}
          />
        </div>

        {/* Content */}
        {displayedGames.length === 0 ? (
          <EmptyState />
        ) : (
          <div
            className={cn(
              "grid gap-4",
              "grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5"
            )}
          >
            {displayedGames.map((game) => (
              <MeepleCard
                key={game.id}
                game={game}
                onWishlistToggle={handleWishlistToggle}
              />
            ))}
          </div>
        )}
      </main>

      <LibraryFloatingPill
        onAddGame={() => {}}
        onFilter={() => {}}
        onSort={() => {}}
        onToggleView={() => setViewMode(viewMode === "grid" ? "list" : "grid")}
      />
    </div>
  )
}
