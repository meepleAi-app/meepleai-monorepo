"use client"

import { useState, useCallback } from "react"
import { Menu } from "lucide-react"
import { Navbar } from "@/components/navbar"
import { ChatSidebar } from "@/components/ai-chat/chat-sidebar"
import { ChatMainArea } from "@/components/ai-chat/chat-main-area"
import { MobileChatDrawer } from "@/components/ai-chat/mobile-chat-drawer"
import { Button } from "@/components/ui/button"

interface Message {
  id: string
  role: "user" | "assistant"
  content: string
  citation?: {
    page: number
    document: string
    excerpt: string
  }
}

// Mock data
const mockChatHistory = [
  {
    id: "1",
    gameEmoji: "🏝️",
    title: "Catan — Regola del commercio",
    timestamp: "Oggi, 14:32",
  },
  {
    id: "2",
    gameEmoji: "🔴",
    title: "Splendor — Carte nobili",
    timestamp: "Ieri, 21:15",
  },
  {
    id: "3",
    gameEmoji: "🐑",
    title: "Agricola — Nutrire la famiglia",
    timestamp: "2 giorni fa",
  },
  {
    id: "4",
    gameEmoji: "🚂",
    title: "Ticket to Ride — Percorsi lunghi",
    timestamp: "5 giorni fa",
  },
]

const mockGamesWithKB = [
  {
    id: "catan",
    name: "Settlers of Catan",
    coverUrl: "https://images.unsplash.com/photo-1632501641765-e568d28b0015?w=64&h=64&fit=crop",
    status: "ready" as const,
  },
  {
    id: "splendor",
    name: "Splendor",
    coverUrl: "https://images.unsplash.com/photo-1606503153255-59d8b8b82176?w=64&h=64&fit=crop",
    status: "ready" as const,
  },
  {
    id: "agricola",
    name: "Agricola",
    coverUrl: "https://images.unsplash.com/photo-1611371805429-8b5c1b2c34ba?w=64&h=64&fit=crop",
    status: "indexing" as const,
  },
  {
    id: "ticket",
    name: "Ticket to Ride",
    coverUrl: "https://images.unsplash.com/photo-1610890716171-6b1bb98ffd09?w=64&h=64&fit=crop",
    status: "ready" as const,
  },
]

// Example AI responses
const aiResponses: Record<string, { content: string; citation?: { page: number; document: string; excerpt: string } }> = {
  "Qual è la regola del commercio?": {
    content: "Nel **commercio** di Catan, puoi scambiare risorse con altri giocatori durante il tuo turno.\n\n### Regole principali:\n- Puoi proporre scambi solo durante il tuo turno\n- Gli altri giocatori possono accettare o rifiutare\n- Puoi anche usare i **porti** per scambiare con la banca a tassi migliori (3:1 o 2:1)",
    citation: {
      page: 34,
      document: "Regolamento Catan.pdf",
      excerpt: "Durante il proprio turno, il giocatore attivo può commerciare con gli altri giocatori proponendo scambi di risorse.",
    },
  },
  "Come si vince?": {
    content: "Per vincere a Catan devi raggiungere **10 punti vittoria**.\n\n### Come ottenerli:\n- **Insediamento**: 1 punto\n- **Città**: 2 punti\n- **Strada più lunga** (min 5 tratti): 2 punti\n- **Esercito più potente** (min 3 cavalieri): 2 punti\n- **Carte sviluppo**: alcuni punti nascosti",
    citation: {
      page: 12,
      document: "Regolamento Catan.pdf",
      excerpt: "Il primo giocatore che raggiunge 10 punti vittoria durante il proprio turno vince la partita.",
    },
  },
  "default": {
    content: "Grazie per la domanda! Basandomi sul regolamento del gioco, posso dirti che questa è una regola importante da ricordare durante le partite.\n\nSe hai bisogno di chiarimenti specifici, non esitare a chiedere!",
  },
}

export default function AIChatPage() {
  const [activeChatId, setActiveChatId] = useState<string | null>("1")
  const [mobileDrawerOpen, setMobileDrawerOpen] = useState(false)
  const [messages, setMessages] = useState<Message[]>([])
  const [isTyping, setIsTyping] = useState(false)
  const [gameContext, setGameContext] = useState<{
    id: string
    name: string
    emoji: string
    pdfCount: number
  } | null>({
    id: "catan",
    name: "Settlers of Catan",
    emoji: "🏝️",
    pdfCount: 3,
  })

  const handleSendMessage = useCallback((content: string) => {
    // Add user message
    const userMessage: Message = {
      id: Date.now().toString(),
      role: "user",
      content,
    }
    setMessages((prev) => [...prev, userMessage])
    setIsTyping(true)

    // Simulate AI response
    setTimeout(() => {
      const response = aiResponses[content] || aiResponses["default"]
      const aiMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: "assistant",
        content: response.content,
        citation: response.citation,
      }
      setMessages((prev) => [...prev, aiMessage])
      setIsTyping(false)
    }, 1500)
  }, [])

  const handleSelectChat = (id: string) => {
    setActiveChatId(id)
    // In a real app, load messages for this chat
    setMessages([])
  }

  const handleNewChat = () => {
    setActiveChatId(null)
    setMessages([])
  }

  const handleSelectGame = (id: string) => {
    const game = mockGamesWithKB.find((g) => g.id === id)
    if (game) {
      setGameContext({
        id: game.id,
        name: game.name,
        emoji: "🎲",
        pdfCount: Math.floor(Math.random() * 3) + 1,
      })
    }
  }

  return (
    <div className="min-h-screen bg-background">
      <Navbar activeLink="" />

      <div className="flex">
        {/* Desktop Sidebar */}
        <ChatSidebar
          chatHistory={mockChatHistory}
          gamesWithKB={mockGamesWithKB}
          activeChatId={activeChatId}
          onSelectChat={handleSelectChat}
          onNewChat={handleNewChat}
          onSelectGame={handleSelectGame}
        />

        {/* Main Chat Area */}
        <div className="flex-1 relative">
          {/* Mobile Menu Button */}
          <Button
            variant="ghost"
            size="icon"
            className="lg:hidden absolute top-3 left-4 z-10 w-9 h-9"
            onClick={() => setMobileDrawerOpen(true)}
          >
            <Menu className="w-5 h-5" />
          </Button>

          <ChatMainArea
            gameContext={gameContext}
            messages={messages}
            onSendMessage={handleSendMessage}
            onSelectGame={() => {}}
            isTyping={isTyping}
          />
        </div>
      </div>

      {/* Mobile Drawer */}
      <MobileChatDrawer
        isOpen={mobileDrawerOpen}
        onClose={() => setMobileDrawerOpen(false)}
        chatHistory={mockChatHistory}
        gamesWithKB={mockGamesWithKB}
        activeChatId={activeChatId}
        onSelectChat={handleSelectChat}
        onNewChat={handleNewChat}
        onSelectGame={handleSelectGame}
      />
    </div>
  )
}
