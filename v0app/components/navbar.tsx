"use client"

import { useState } from "react"
import { Search, Menu, X } from "lucide-react"
import { cn } from "@/lib/utils"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"

const navLinks = [
  { label: "Libreria", href: "/libreria" },
  { label: "Catalogo", href: "/catalogo" },
  { label: "Sessioni", href: "/sessioni" },
]

interface NavbarProps {
  activeLink?: string
}

export function Navbar({ activeLink = "Catalogo" }: NavbarProps) {
  const [searchExpanded, setSearchExpanded] = useState(false)
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)

  return (
    <header className="sticky top-0 z-50 h-[52px] bg-[#0f172a]/95 backdrop-blur-md border-b border-border/50">
      <div className="h-full max-w-[1200px] mx-auto px-4 flex items-center justify-between">
        {/* Left: Logo */}
        <div className="flex items-center gap-3">
          <div className="relative w-8 h-8">
            {/* Hexagonal logo with purple gradient */}
            <svg viewBox="0 0 32 32" className="w-full h-full">
              <defs>
                <linearGradient id="logoGradient" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" stopColor="#6366f1" />
                  <stop offset="100%" stopColor="#8b5cf6" />
                </linearGradient>
              </defs>
              <path
                d="M16 2 L28 9 L28 23 L16 30 L4 23 L4 9 Z"
                fill="url(#logoGradient)"
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
        </div>

        {/* Center: Nav Links (desktop) */}
        <nav className="hidden sm:flex items-center gap-1">
          {navLinks.map((link) => (
            <a
              key={link.label}
              href={link.href}
              className={cn(
                "px-4 py-1.5 rounded-lg font-body font-semibold text-sm transition-colors",
                link.label === activeLink
                  ? "bg-muted text-foreground"
                  : "text-muted-foreground hover:text-foreground hover:bg-muted/50"
              )}
            >
              {link.label}
            </a>
          ))}
        </nav>

        {/* Right: Search + Avatar */}
        <div className="flex items-center gap-3">
          {/* Search bar */}
          <div className="relative hidden sm:flex items-center">
            <div
              className={cn(
                "flex items-center transition-all duration-200 ease-out",
                searchExpanded ? "w-64" : "w-9"
              )}
            >
              {searchExpanded ? (
                <div className="relative w-full">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                  <Input
                    type="text"
                    placeholder="Cerca giochi..."
                    className="pl-9 pr-3 h-9 bg-muted border-border text-sm font-body"
                    autoFocus
                    onBlur={() => setSearchExpanded(false)}
                  />
                </div>
              ) : (
                <Button
                  variant="ghost"
                  size="icon"
                  className="w-9 h-9 text-muted-foreground hover:text-foreground"
                  onClick={() => setSearchExpanded(true)}
                >
                  <Search className="w-4 h-4" />
                </Button>
              )}
            </div>
          </div>

          {/* User Avatar */}
          <Avatar className="w-8 h-8 border-2 border-entity-player/50">
            <AvatarImage src="/avatar.jpg" alt="User" />
            <AvatarFallback className="bg-entity-player text-white text-xs font-body font-bold">
              MR
            </AvatarFallback>
          </Avatar>

          {/* Mobile menu button */}
          <Button
            variant="ghost"
            size="icon"
            className="sm:hidden w-9 h-9 text-muted-foreground"
            onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
          >
            {mobileMenuOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
          </Button>
        </div>
      </div>

      {/* Mobile menu */}
      {mobileMenuOpen && (
        <div className="sm:hidden absolute top-[52px] left-0 right-0 bg-card border-b border-border p-4">
          <nav className="flex flex-col gap-2">
            {navLinks.map((link) => (
              <a
                key={link.label}
                href={link.href}
                className={cn(
                  "px-4 py-2 rounded-lg font-body font-semibold text-sm",
                  link.label === activeLink
                    ? "bg-muted text-foreground"
                    : "text-muted-foreground hover:text-foreground hover:bg-muted/50"
                )}
              >
                {link.label}
              </a>
            ))}
            {/* Mobile search */}
            <div className="relative mt-2">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
              <Input
                type="text"
                placeholder="Cerca giochi..."
                className="pl-9 pr-3 h-10 bg-muted border-border text-sm font-body"
              />
            </div>
          </nav>
        </div>
      )}
    </header>
  )
}
