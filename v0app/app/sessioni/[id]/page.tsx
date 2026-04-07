"use client"

import { useState, useEffect } from "react"
import { Navbar } from "@/components/navbar"
import { SessionMiniNav } from "@/components/session/session-mini-nav"
import { ScoreTracker } from "@/components/session/score-tracker"
import { AIChatPanel } from "@/components/session/ai-chat-panel"
import { SessionFloatingPill } from "@/components/session/session-floating-pill"
import { PausedOverlay, EndedOverlay } from "@/components/session/session-overlays"
import { MobileAISheet } from "@/components/session/mobile-ai-sheet"

// Session state type
type SessionState = "in_progress" | "paused" | "ended"

// Mock data
const initialPlayers = [
  { id: "1", name: "Marco Rossi", initials: "MR", score: 12, color: "hsl(262, 83%, 58%)" },
  { id: "2", name: "Laura Bianchi", initials: "LB", score: 9, color: "hsl(340, 82%, 52%)" },
  { id: "3", name: "Giovanni Verdi", initials: "GV", score: 7, color: "hsl(142, 71%, 45%)" },
  { id: "4", name: "Sofia Neri", initials: "SN", score: 5, color: "hsl(217, 91%, 60%)" },
]

const initialMessages = [
  {
    id: "1",
    role: "assistant" as const,
    content: "Ciao! Sono il tuo assistente AI per questa partita a Settlers of Catan. Posso aiutarti con le regole, rispondere a domande o chiarire dubbi. Chiedi pure!",
  },
  {
    id: "2",
    role: "user" as const,
    content: "Come funziona lo scambio marittimo?",
  },
  {
    id: "3",
    role: "assistant" as const,
    content: "Lo scambio marittimo ti permette di scambiare risorse con la banca secondo queste regole:\n\n• **Porto generico (3:1)**: 3 risorse uguali per 1 risorsa a scelta\n• **Porto specifico (2:1)**: 2 risorse del tipo indicato per 1 risorsa a scelta\n• **Senza porto (4:1)**: 4 risorse uguali per 1 risorsa a scelta",
    citation: {
      page: 34,
      document: "Regolamento.pdf",
      excerpt: "Il giocatore può sempre scambiare 4:1 con la banca, oppure usare i porti per ottenere tassi migliori...",
    },
  },
]

export default function ActiveSessionPage() {
  const [activeTab, setActiveTab] = useState("punteggi")
  const [sessionState, setSessionState] = useState<SessionState>("in_progress")
  const [players, setPlayers] = useState(initialPlayers)
  const [messages, setMessages] = useState(initialMessages)
  const [isChatCollapsed, setIsChatCollapsed] = useState(false)
  const [isMobileAIOpen, setIsMobileAIOpen] = useState(false)
  const [isTyping, setIsTyping] = useState(false)
  const [elapsedSeconds, setElapsedSeconds] = useState(45 * 60) // Start at 45 minutes

  // Timer effect
  useEffect(() => {
    if (sessionState !== "in_progress") return

    const interval = setInterval(() => {
      setElapsedSeconds((prev) => prev + 1)
    }, 1000)

    return () => clearInterval(interval)
  }, [sessionState])

  // Format duration
  const formatDuration = (seconds: number) => {
    const hours = Math.floor(seconds / 3600)
    const mins = Math.floor((seconds % 3600) / 60)
    const secs = seconds % 60
    
    if (hours > 0) {
      return `${hours}:${mins.toString().padStart(2, "0")}:${secs.toString().padStart(2, "0")}`
    }
    return `${mins}:${secs.toString().padStart(2, "0")}`
  }

  const duration = formatDuration(elapsedSeconds)

  // Handlers
  const handleScoreChange = (playerId: string, delta: number) => {
    setPlayers((prev) =>
      prev.map((p) =>
        p.id === playerId ? { ...p, score: Math.max(0, p.score + delta) } : p
      )
    )
  }

  const handleAddPlayer = () => {
    const newId = String(players.length + 1)
    const colors = ["hsl(38, 92%, 50%)", "hsl(350, 89%, 60%)", "hsl(210, 40%, 55%)"]
    setPlayers([
      ...players,
      {
        id: newId,
        name: `Giocatore ${newId}`,
        initials: `G${newId}`,
        score: 0,
        color: colors[players.length % colors.length],
      },
    ])
  }

  const handleSendMessage = (content: string) => {
    const userMessage = {
      id: String(messages.length + 1),
      role: "user" as const,
      content,
    }
    setMessages((prev) => [...prev, userMessage])
    setIsTyping(true)

    // Simulate AI response
    setTimeout(() => {
      setIsTyping(false)
      const aiMessage = {
        id: String(messages.length + 2),
        role: "assistant" as const,
        content: "Ottima domanda! In base al regolamento, la risposta dipende dalla situazione specifica. Vuoi che ti spieghi nel dettaglio?",
      }
      setMessages((prev) => [...prev, aiMessage])
    }, 1500)
  }

  const handleTogglePause = () => {
    setSessionState((prev) => (prev === "paused" ? "in_progress" : "paused"))
  }

  const handleEndSession = () => {
    setSessionState("ended")
  }

  const handleSaveAndShare = () => {
    // Would save and share the session
    alert("Sessione salvata e pronta per la condivisione!")
  }

  const handleCloseEnded = () => {
    // Navigate back to sessions list
    window.location.href = "/sessioni"
  }

  return (
    <div className="min-h-screen bg-background">
      <Navbar activeLink="Sessioni" />
      <SessionMiniNav activeTab={activeTab} onTabChange={setActiveTab} />

      {/* Main Content */}
      <div className="flex">
        {/* Left Column - Score Tracker */}
        <main className="flex-1 max-w-[1200px] mx-auto px-4 py-6 pb-32 lg:pb-24">
          <ScoreTracker
            gameName="Settlers of Catan"
            gameCover="https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__itemrep/img/IzYEUm_gWFuRFOL8gQYqGm5gU6A=/fit-in/246x300/filters:strip_icc()/pic2419375.jpg"
            duration={duration}
            players={players}
            onScoreChange={handleScoreChange}
            onAddPlayer={handleAddPlayer}
          />
        </main>

        {/* Right Column - AI Chat Panel (Desktop only) */}
        <AIChatPanel
          gameName="Settlers of Catan"
          documentsCount={3}
          isCollapsed={isChatCollapsed}
          onToggleCollapse={() => setIsChatCollapsed(!isChatCollapsed)}
          messages={messages}
          onSendMessage={handleSendMessage}
          isTyping={isTyping}
        />
      </div>

      {/* Floating Action Pill */}
      <SessionFloatingPill
        duration={duration}
        isPaused={sessionState === "paused"}
        onTogglePause={handleTogglePause}
        onEndSession={handleEndSession}
        onOpenAI={() => setIsMobileAIOpen(true)}
        onOpenTimer={() => setActiveTab("timer")}
        onOpenNotes={() => setActiveTab("note")}
      />

      {/* Mobile AI Sheet */}
      <MobileAISheet
        isOpen={isMobileAIOpen}
        onClose={() => setIsMobileAIOpen(false)}
        gameName="Settlers of Catan"
        documentsCount={3}
        messages={messages}
        onSendMessage={handleSendMessage}
        isTyping={isTyping}
      />

      {/* Session State Overlays */}
      {sessionState === "paused" && (
        <PausedOverlay onResume={() => setSessionState("in_progress")} />
      )}

      {sessionState === "ended" && (
        <EndedOverlay
          players={players}
          gameName="Settlers of Catan"
          duration={duration}
          onSaveAndShare={handleSaveAndShare}
          onClose={handleCloseEnded}
        />
      )}
    </div>
  )
}
