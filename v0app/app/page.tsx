"use client"

import { useState } from "react"
import { Navbar } from "@/components/navbar"
import { MiniNav } from "@/components/mini-nav"
import { FloatingActionPill } from "@/components/floating-action-pill"
import { GameHeader } from "@/components/game-detail/game-header"
import { StatsRow } from "@/components/game-detail/stats-row"
import { DocumentsSection } from "@/components/game-detail/documents-section"
import { SessionsSection } from "@/components/game-detail/sessions-section"

const gameData = {
  title: "Settlers of Catan",
  players: "3–4 giocatori",
  duration: "90 min",
  rating: "7.8",
}

export default function GameDetailPage() {
  const [activeTab, setActiveTab] = useState("panoramica")

  return (
    <div className="min-h-screen bg-background">
      <Navbar activeLink="Catalogo" />
      <MiniNav activeTab={activeTab} onTabChange={setActiveTab} />

      {/* Main content */}
      <main className="max-w-[1100px] mx-auto px-4 sm:px-6 py-6 sm:py-8 pb-32 sm:pb-24">
        {/* Mobile breadcrumb spacer */}
        <div className="h-10 sm:hidden" />

        <div className="space-y-8">
          {/* Game Header */}
          <GameHeader
            title={gameData.title}
            players={gameData.players}
            duration={gameData.duration}
            rating={gameData.rating}
          />

          {/* Stats Row */}
          <StatsRow />

          {/* Documents Section */}
          <DocumentsSection />

          {/* Sessions Section */}
          <SessionsSection />
        </div>
      </main>

      {/* Floating action pill / mobile action bar */}
      <FloatingActionPill gameName={gameData.title} />
    </div>
  )
}
