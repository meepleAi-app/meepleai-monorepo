"use client"

import { useState } from "react"
import { Menu, X } from "lucide-react"
import { Button } from "@/components/ui/button"

const navLinks = [
  { label: "Come funziona", href: "#come-funziona" },
  { label: "Catalogo", href: "#catalogo" },
  { label: "FAQ", href: "#faq" },
]

export function LandingNavbar() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)

  return (
    <header className="sticky top-0 z-50 h-[56px] bg-[#0f172a]/95 backdrop-blur-md border-b border-border/50">
      <div className="h-full max-w-[1200px] mx-auto px-4 flex items-center justify-between">
        {/* Left: Logo */}
        <a href="/" className="flex items-center gap-3">
          <div className="relative w-8 h-8">
            <svg viewBox="0 0 32 32" className="w-full h-full">
              <defs>
                <linearGradient id="logoGradientLanding" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" stopColor="#6366f1" />
                  <stop offset="100%" stopColor="#8b5cf6" />
                </linearGradient>
              </defs>
              <path
                d="M16 2 L28 9 L28 23 L16 30 L4 23 L4 9 Z"
                fill="url(#logoGradientLanding)"
              />
              <path
                d="M12 12 L16 10 L20 12 L20 18 L16 20 L12 18 Z"
                fill="white"
                opacity="0.9"
              />
            </svg>
          </div>
          <span className="font-heading font-bold text-lg text-foreground">
            Meeple<span className="text-entity-game">Ai</span>
          </span>
        </a>

        {/* Center: Nav Links (desktop) */}
        <nav className="hidden md:flex items-center gap-1">
          {navLinks.map((link) => (
            <a
              key={link.label}
              href={link.href}
              className="px-4 py-1.5 rounded-lg font-body font-semibold text-sm text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
            >
              {link.label}
            </a>
          ))}
        </nav>

        {/* Right: Auth buttons */}
        <div className="hidden md:flex items-center gap-3">
          <Button
            variant="ghost"
            className="font-body font-semibold text-sm text-muted-foreground hover:text-foreground"
          >
            Accedi
          </Button>
          <Button className="font-body font-bold text-sm bg-entity-game hover:bg-entity-game/90 text-white">
            Inizia gratis
          </Button>
        </div>

        {/* Mobile menu button */}
        <Button
          variant="ghost"
          size="icon"
          className="md:hidden w-9 h-9 text-muted-foreground"
          onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
        >
          {mobileMenuOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
        </Button>
      </div>

      {/* Mobile menu */}
      {mobileMenuOpen && (
        <div className="md:hidden absolute top-[56px] left-0 right-0 bg-card border-b border-border p-4">
          <nav className="flex flex-col gap-2">
            {navLinks.map((link) => (
              <a
                key={link.label}
                href={link.href}
                className="px-4 py-2 rounded-lg font-body font-semibold text-sm text-muted-foreground hover:text-foreground hover:bg-muted/50"
                onClick={() => setMobileMenuOpen(false)}
              >
                {link.label}
              </a>
            ))}
            <div className="flex flex-col gap-2 mt-4 pt-4 border-t border-border">
              <Button
                variant="ghost"
                className="w-full justify-center font-body font-semibold text-sm"
              >
                Accedi
              </Button>
              <Button className="w-full justify-center font-body font-bold text-sm bg-entity-game hover:bg-entity-game/90 text-white">
                Inizia gratis
              </Button>
            </div>
          </nav>
        </div>
      )}
    </header>
  )
}
