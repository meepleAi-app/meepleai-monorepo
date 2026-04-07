export function LandingFooter() {
  const links = [
    { label: "Come funziona", href: "#come-funziona" },
    { label: "Catalogo", href: "#catalogo" },
    { label: "FAQ", href: "#faq" },
    { label: "Privacy", href: "/privacy" },
    { label: "Termini", href: "/termini" },
  ]

  return (
    <footer className="bg-[#0f172a] border-t border-border/50 py-10">
      <div className="max-w-[1200px] mx-auto px-4">
        <div className="flex flex-col md:flex-row items-center justify-between gap-6">
          {/* Logo + tagline */}
          <div className="flex flex-col items-center md:items-start gap-2">
            <div className="flex items-center gap-3">
              <div className="relative w-7 h-7">
                <svg viewBox="0 0 32 32" className="w-full h-full">
                  <defs>
                    <linearGradient id="footerLogoGradient" x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stopColor="#6366f1" />
                      <stop offset="100%" stopColor="#8b5cf6" />
                    </linearGradient>
                  </defs>
                  <path
                    d="M16 2 L28 9 L28 23 L16 30 L4 23 L4 9 Z"
                    fill="url(#footerLogoGradient)"
                  />
                  <path
                    d="M12 12 L16 10 L20 12 L20 18 L16 20 L12 18 Z"
                    fill="white"
                    opacity="0.9"
                  />
                </svg>
              </div>
              <span className="font-heading font-bold text-base text-foreground">
                Meeple<span className="text-entity-game">Ai</span>
              </span>
            </div>
            <p className="font-body text-sm text-muted-foreground text-center md:text-left">
              Il tuo compagno AI al tavolo da gioco
            </p>
          </div>

          {/* Links */}
          <nav className="flex flex-wrap items-center justify-center gap-x-6 gap-y-2">
            {links.map((link, index) => (
              <a
                key={link.label}
                href={link.href}
                className="font-body text-sm text-muted-foreground hover:text-foreground transition-colors"
              >
                {link.label}
              </a>
            ))}
          </nav>
        </div>

        {/* Copyright */}
        <div className="mt-8 pt-6 border-t border-border/30 text-center">
          <p className="font-body text-xs text-muted-foreground/60">
            © 2026 MeepleAi. Tutti i diritti riservati.
          </p>
        </div>
      </div>
    </footer>
  )
}
