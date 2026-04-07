"use client"

import { useState } from "react"
import { X, Plus, MessageSquare, Dices, ChevronDown, ChevronRight } from "lucide-react"
import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"

interface ChatHistoryItem {
  id: string
  gameEmoji: string
  title: string
  timestamp: string
}

interface GameWithKB {
  id: string
  name: string
  coverUrl: string
  status: "ready" | "indexing"
}

interface MobileChatDrawerProps {
  isOpen: boolean
  onClose: () => void
  chatHistory: ChatHistoryItem[]
  gamesWithKB: GameWithKB[]
  activeChatId: string | null
  onSelectChat: (id: string) => void
  onNewChat: () => void
  onSelectGame: (id: string) => void
}

export function MobileChatDrawer({
  isOpen,
  onClose,
  chatHistory,
  gamesWithKB,
  activeChatId,
  onSelectChat,
  onNewChat,
  onSelectGame,
}: MobileChatDrawerProps) {
  const [chatHistoryExpanded, setChatHistoryExpanded] = useState(true)
  const [gamesExpanded, setGamesExpanded] = useState(true)

  if (!isOpen) return null

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black/60 z-50 lg:hidden"
        onClick={onClose}
      />

      {/* Drawer */}
      <div className="fixed inset-y-0 left-0 w-[280px] bg-card border-r border-border z-50 lg:hidden flex flex-col animate-in slide-in-from-left duration-200">
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b border-border">
          <span className="font-heading font-bold text-base text-foreground">
            Chat
          </span>
          <Button
            variant="ghost"
            size="icon"
            className="w-8 h-8"
            onClick={onClose}
          >
            <X className="w-4 h-4" />
          </Button>
        </div>

        {/* New Chat Button */}
        <div className="p-4">
          <Button
            onClick={() => {
              onNewChat()
              onClose()
            }}
            className="w-full bg-entity-game hover:bg-entity-game/90 text-white font-body font-semibold"
          >
            <Plus className="w-4 h-4 mr-2" />
            Nuova chat
          </Button>
        </div>

        {/* Scrollable Content */}
        <div className="flex-1 overflow-y-auto">
          {/* Chat History Section */}
          <div className="px-4 pb-4">
            <button
              onClick={() => setChatHistoryExpanded(!chatHistoryExpanded)}
              className="flex items-center gap-2 w-full py-2 text-left"
            >
              {chatHistoryExpanded ? (
                <ChevronDown className="w-4 h-4 text-muted-foreground" />
              ) : (
                <ChevronRight className="w-4 h-4 text-muted-foreground" />
              )}
              <span className="text-xs font-body font-semibold text-muted-foreground uppercase tracking-wide">
                Chat recenti
              </span>
            </button>

            {chatHistoryExpanded && (
              <div className="flex flex-col gap-1 mt-1">
                {chatHistory.map((chat) => (
                  <button
                    key={chat.id}
                    onClick={() => {
                      onSelectChat(chat.id)
                      onClose()
                    }}
                    className={cn(
                      "flex items-start gap-3 w-full p-2.5 rounded-lg text-left transition-colors relative",
                      activeChatId === chat.id
                        ? "bg-muted"
                        : "hover:bg-muted/50"
                    )}
                  >
                    {activeChatId === chat.id && (
                      <div className="absolute left-0 top-1/2 -translate-y-1/2 w-1 h-6 bg-entity-game rounded-r-full" />
                    )}
                    
                    <span className="text-base flex-shrink-0">{chat.gameEmoji}</span>
                    <div className="flex-1 min-w-0">
                      <p className="font-body text-sm text-foreground truncate">
                        {chat.title}
                      </p>
                      <p className="font-body text-xs text-muted-foreground mt-0.5">
                        {chat.timestamp}
                      </p>
                    </div>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Divider */}
          <div className="h-px bg-border mx-4" />

          {/* Games with KB Section */}
          <div className="px-4 py-4">
            <button
              onClick={() => setGamesExpanded(!gamesExpanded)}
              className="flex items-center gap-2 w-full py-2 text-left"
            >
              {gamesExpanded ? (
                <ChevronDown className="w-4 h-4 text-muted-foreground" />
              ) : (
                <ChevronRight className="w-4 h-4 text-muted-foreground" />
              )}
              <span className="text-xs font-body font-semibold text-muted-foreground uppercase tracking-wide">
                Giochi con KB
              </span>
            </button>

            {gamesExpanded && (
              <div className="flex flex-col gap-1 mt-1">
                {gamesWithKB.map((game) => (
                  <button
                    key={game.id}
                    onClick={() => {
                      onSelectGame(game.id)
                      onClose()
                    }}
                    className="flex items-center gap-3 w-full p-2 rounded-lg text-left hover:bg-muted/50 transition-colors"
                  >
                    <div className="w-8 h-8 rounded-md overflow-hidden bg-muted flex-shrink-0">
                      <img
                        src={game.coverUrl}
                        alt={game.name}
                        className="w-full h-full object-cover"
                      />
                    </div>
                    <span className="font-body text-sm text-foreground truncate flex-1">
                      {game.name}
                    </span>
                    <span
                      className={cn(
                        "w-2 h-2 rounded-full flex-shrink-0",
                        game.status === "ready" ? "bg-green-500" : "bg-yellow-500"
                      )}
                    />
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </>
  )
}
