"use client"

import { cn } from "@/lib/utils"

interface SessionMiniNavProps {
  activeTab: string
  onTabChange: (tab: string) => void
}

const tabs = [
  { id: "punteggi", label: "Punteggi" },
  { id: "note", label: "Note" },
  { id: "timer", label: "Timer" },
  { id: "impostazioni", label: "Impostazioni" },
]

export function SessionMiniNav({ activeTab, onTabChange }: SessionMiniNavProps) {
  return (
    <div className="sticky top-[52px] z-40 h-10 bg-card border-b border-border/50">
      <div className="h-full max-w-[1200px] mx-auto px-4 flex items-center gap-1">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            onClick={() => onTabChange(tab.id)}
            className={cn(
              "px-4 py-1.5 rounded-md font-body font-semibold text-sm transition-all",
              activeTab === tab.id
                ? "bg-entity-session/15 text-entity-session"
                : "text-muted-foreground hover:text-foreground hover:bg-muted/50"
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>
    </div>
  )
}
