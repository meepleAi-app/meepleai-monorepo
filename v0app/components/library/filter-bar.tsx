"use client"

import { ChevronDown } from "lucide-react"
import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

interface FilterBarProps {
  activeFilter: string
  onFilterChange: (filter: string) => void
  activePlayerFilter: string | null
  onPlayerFilterChange: (filter: string | null) => void
  sortBy: string
  onSortChange: (sort: string) => void
}

const collectionFilters = ["Tutti", "Posseduti", "Wishlist"]
const playerFilters = ["2", "3", "4", "5+"]
const sortOptions = [
  { label: "A-Z", value: "az" },
  { label: "Ultima giocata", value: "lastPlayed" },
  { label: "Valutazione", value: "rating" },
  { label: "Aggiunto", value: "added" },
]

export function FilterBar({
  activeFilter,
  onFilterChange,
  activePlayerFilter,
  onPlayerFilterChange,
  sortBy,
  onSortChange,
}: FilterBarProps) {
  const currentSort = sortOptions.find((s) => s.value === sortBy)

  return (
    <div className="flex items-center gap-2 overflow-x-auto pb-2 scrollbar-hide">
      {/* Collection filters */}
      {collectionFilters.map((filter) => (
        <button
          key={filter}
          onClick={() => onFilterChange(filter)}
          className={cn(
            "px-3 py-1.5 rounded-full font-body font-semibold text-xs whitespace-nowrap transition-colors",
            activeFilter === filter
              ? "bg-entity-game text-white"
              : "bg-muted text-muted-foreground hover:text-foreground hover:bg-muted/80"
          )}
        >
          {filter}
        </button>
      ))}

      {/* Divider */}
      <div className="w-px h-5 bg-border/50 mx-1" />

      {/* Player count filters */}
      {playerFilters.map((count) => (
        <button
          key={count}
          onClick={() =>
            onPlayerFilterChange(activePlayerFilter === count ? null : count)
          }
          className={cn(
            "px-3 py-1.5 rounded-full font-body font-semibold text-xs whitespace-nowrap transition-colors",
            activePlayerFilter === count
              ? "bg-entity-player text-white"
              : "bg-muted text-muted-foreground hover:text-foreground hover:bg-muted/80"
          )}
        >
          {count}p
        </button>
      ))}

      {/* Divider */}
      <div className="w-px h-5 bg-border/50 mx-1" />

      {/* Sort dropdown */}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="sm"
            className="h-7 px-3 rounded-full bg-muted text-muted-foreground hover:text-foreground font-body font-semibold text-xs gap-1"
          >
            {currentSort?.label}
            <ChevronDown className="w-3.5 h-3.5" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="min-w-[140px]">
          {sortOptions.map((option) => (
            <DropdownMenuItem
              key={option.value}
              onClick={() => onSortChange(option.value)}
              className={cn(
                "font-body text-sm",
                sortBy === option.value && "bg-muted"
              )}
            >
              {option.label}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  )
}
