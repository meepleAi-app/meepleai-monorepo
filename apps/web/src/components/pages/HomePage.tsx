'use client';

/**
 * Home Page Client Component
 *
 * Landing page for MeepleAI with hero section, features, testimonials, and CTA.
 * Extracted from Pages Router for App Router compatibility.
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router
 */

import { useState } from "react";
import Link from "next/link";
import dynamic from "next/dynamic";
import { useRouter } from "next/navigation"; // App Router uses next/navigation
import { motion } from "framer-motion";
import { useInView } from "react-intersection-observer";
import { ThemeSwitcher } from "@/components/ThemeSwitcher";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { AuthModal } from "@/components/auth";
import { useAuth } from "@/hooks/useAuth";

// Lazy load below-fold sections
const FeaturesSection = dynamic(() => import("@/components/landing/FeaturesSection"), {
  loading: () => <div className="py-20 px-6" />,
});
const KeyFeaturesSection = dynamic(() => import("@/components/landing/KeyFeaturesSection"), {
  loading: () => <div className="py-20 px-6" />,
});
const TestimonialsSection = dynamic(() => import("@/components/landing/TestimonialsSection"), {
  loading: () => <div className="py-20 px-6" />,
});

export default function HomePage() {
  const router = useRouter();
  const { user: authUser, logout } = useAuth();
  const [showAuthModal, setShowAuthModal] = useState(false);
  const [demoEmail, setDemoEmail] = useState("");
  const [demoPassword, setDemoPassword] = useState("");

  // Consolidated Intersection Observer with multiple thresholds
  const { ref: heroRef, inView: heroInView } = useInView({
    triggerOnce: true,
    threshold: [0.1, 0.5, 0.9],
  });

  // Handler for "Try Demo" button
  const handleTryDemo = () => {
    setDemoEmail("user@meepleai.dev");
    setDemoPassword("Demo123!");
    setShowAuthModal(true);
  };

  return (
    <div className="min-h-screen bg-slate-950 text-white">
      {/* Header */}
      <header className="sticky top-0 glass z-50">
        <div className="max-w-7xl mx-auto px-6 py-4 flex justify-between items-center">
          <Link href="/" className="flex items-center gap-3 hover:opacity-80 transition-opacity">
            <span className="text-4xl">🎲</span>
            <span className="text-2xl font-bold gradient-text">MeepleAI</span>
          </Link>

          {/* Navigation and theme switcher */}
          <div className="flex items-center gap-4">
            {/* Theme switcher - always visible on all screen sizes */}
            <ThemeSwitcher />

            {/* Navigation links - hidden on mobile */}
            <nav aria-label="Main navigation" className="hidden md:flex items-center gap-6">
              {authUser ? (
                <>
                  <Link href="/chat" className="text-slate-300 hover:text-white transition-colors">
                    Chat
                  </Link>
                  <Link href="/chess" className="text-slate-300 hover:text-white transition-colors">
                    Chess
                  </Link>
                  <Link href="/upload" className="text-slate-300 hover:text-white transition-colors">
                    Upload
                  </Link>
                  {authUser.role === "Admin" && (
                    <Link href="/admin" className="text-slate-300 hover:text-white transition-colors">
                      Admin
                    </Link>
                  )}
                  <button
                    onClick={logout}
                    className="inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md text-sm font-medium transition-colors border border-input bg-background shadow-sm hover:bg-accent hover:text-accent-foreground h-9 py-2 px-4"
                    aria-label="Logout from MeepleAI"
                  >
                    Logout
                  </button>
                </>
              ) : (
                <Button onClick={() => setShowAuthModal(true)} data-testid="nav-get-started">
                  Get Started
                </Button>
              )}
            </nav>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main id="main-content">
        {/* Hero Section */}
        <section ref={heroRef} className="relative min-h-screen flex items-center px-6 py-20">
        <div className="max-w-7xl mx-auto w-full grid md:grid-cols-2 gap-12 items-center">
          {/* Hero Content */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={heroInView ? { opacity: 1, y: 0 } : {}}
            transition={{ duration: 0.6 }}
            className="space-y-6"
          >
            <h1 className="text-5xl md:text-7xl font-extrabold leading-tight">
              Your AI-Powered
              <br />
              <span className="gradient-text-accessible">Board Game Rules Assistant</span>
            </h1>
            <p className="text-xl text-slate-50 leading-relaxed">
              Never argue about rules again. Get instant, accurate answers from any game&apos;s rulebook with AI-powered semantic search.
            </p>
            <div className="flex flex-wrap gap-4">
              <motion.div
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
              >
                <Button
                  onClick={() => authUser ? router.push("/chat") : setShowAuthModal(true)}
                  className="text-lg"
                  data-testid="hero-get-started"
                >
                  {authUser ? "Go to Chat" : "Get Started Free"}
                </Button>
              </motion.div>
              {!authUser && (
                <motion.div
                  whileHover={{ scale: 1.05 }}
                  whileTap={{ scale: 0.95 }}
                >
                  <Button
                    variant="outline"
                    className="text-lg"
                    onClick={handleTryDemo}
                    data-testid="hero-try-demo"
                  >
                    Try Demo Account
                  </Button>
                </motion.div>
              )}
              <motion.div
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
              >
                <Button
                  variant="outline"
                  className="text-lg"
                  asChild
                >
                  <a href="#features">
                    See How It Works
                  </a>
                </Button>
              </motion.div>
            </div>
          </motion.div>

          {/* Hero Visual - improved mobile visibility */}
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={heroInView ? { opacity: 1, scale: 1 } : {}}
            transition={{ duration: 0.6, delay: 0.2 }}
            className="block"
          >
            <Card className="p-6 shadow-2xl shadow-primary/20">
              <div className="space-y-4">
                <motion.div
                  initial={{ opacity: 0, x: 20 }}
                  animate={heroInView ? { opacity: 1, x: 0 } : {}}
                  transition={{ duration: 0.4, delay: 0.4 }}
                  className="flex justify-end"
                >
                  <div className="bg-primary text-primary-foreground px-4 py-3 rounded-2xl max-w-[80%]">
                    How does en passant work in chess?
                  </div>
                </motion.div>
                <motion.div
                  initial={{ opacity: 0, x: -20 }}
                  animate={heroInView ? { opacity: 1, x: 0 } : {}}
                  transition={{ duration: 0.4, delay: 0.6 }}
                  className="space-y-2"
                >
                  <Card className="p-4">
                    <p className="text-sm">
                      <strong>🤖 MeepleAI:</strong> En passant is a special pawn capture that can only occur immediately after a pawn moves two squares forward from its starting position and lands beside an opponent&apos;s pawn...
                    </p>
                    <p className="text-xs text-slate-300 mt-2 italic">
                      📖 Sources: Chess Rules (FIDE) - Page 12
                    </p>
                  </Card>
                </motion.div>
              </div>
            </Card>
          </motion.div>
        </div>

        {/* Scroll Indicator */}
        <motion.div
          className="absolute bottom-10 left-1/2 transform -translate-x-1/2"
          animate={{ y: [0, 10, 0] }}
          transition={{ duration: 2, repeat: Infinity }}
        >
          <svg className="w-6 h-6 text-slate-300" fill="none" strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" viewBox="0 0 24 24" stroke="currentColor">
            <path d="M19 14l-7 7m0 0l-7-7m7 7V3"></path>
          </svg>
        </motion.div>
      </section>

      {/* Lazy-loaded Features Section */}
      <FeaturesSection />

      {/* Lazy-loaded Key Features Section */}
      <KeyFeaturesSection />

      {/* Lazy-loaded Testimonials Section */}
      <TestimonialsSection />

      {/* CTA Section */}
      <section className="py-20 px-6 bg-gradient-cta">
        <motion.div
          initial={{ opacity: 0, scale: 0.95 }}
          whileInView={{ opacity: 1, scale: 1 }}
          transition={{ duration: 0.6 }}
          viewport={{ once: true }}
          className="max-w-4xl mx-auto text-center space-y-6"
        >
          <h2 className="text-5xl font-bold">Ready to Stop Arguing About Rules?</h2>
          <p className="text-xl opacity-90">Join board game enthusiasts using AI to understand rules better</p>
          <motion.div
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
          >
            <Button
              onClick={() => authUser ? router.push("/chat") : setShowAuthModal(true)}
              className="text-lg bg-white text-primary hover:bg-slate-100"
              data-testid="cta-get-started"
            >
              {authUser ? "Start Chatting" : "Get Started Free"}
            </Button>
          </motion.div>
        </motion.div>
      </section>
      </main>

      {/* Footer */}
      <footer className="py-16 px-6 bg-slate-950 border-t border-white/10">
        <div className="max-w-6xl mx-auto grid md:grid-cols-4 gap-12 mb-12">
          <div className="space-y-4">
            <div className="flex items-center gap-3">
              <span className="text-3xl">🎲</span>
              <span className="text-xl font-bold gradient-text-accessible">MeepleAI</span>
            </div>
            <p className="text-slate-300 text-sm">AI-powered board game rules assistance</p>
          </div>

          <div className="space-y-3">
            <h4 className="font-semibold">Product</h4>
            <div className="flex flex-col gap-2 text-sm">
              <Link href="/chat" className="text-slate-300 hover:text-white transition-colors">Chat</Link>
              <Link href="/upload" className="text-slate-300 hover:text-white transition-colors">Upload PDF</Link>
              <Link href="/editor" className="text-slate-300 hover:text-white transition-colors">RuleSpec Editor</Link>
            </div>
          </div>

          <div className="space-y-3">
            <h4 className="font-semibold">Resources</h4>
            <div className="flex flex-col gap-2 text-sm">
              <a href="https://github.com/yourusername/meepleai" target="_blank" rel="noopener noreferrer" className="text-slate-300 hover:text-white transition-colors">GitHub</a>
              <Link href="/docs" className="text-slate-300 hover:text-white transition-colors">Documentation</Link>
              <Link href="/logs" className="text-slate-300 hover:text-white transition-colors">API Logs</Link>
            </div>
          </div>

          <div className="space-y-3">
            <h4 className="font-semibold">Demo Accounts</h4>
            <div className="text-sm text-slate-300 space-y-1">
              <p>admin@meepleai.dev</p>
              <p>editor@meepleai.dev</p>
              <p>user@meepleai.dev</p>
              <p className="font-mono">Password: Demo123!</p>
            </div>
          </div>
        </div>

        <div className="max-w-6xl mx-auto pt-8 border-t border-white/10 text-center text-sm text-slate-300">
          <p>© 2025 MeepleAI. Open source project.</p>
        </div>
      </footer>

      {/* Unified Auth Modal */}
      <AuthModal
        isOpen={showAuthModal}
        onClose={() => {
          setShowAuthModal(false);
          setDemoEmail("");
          setDemoPassword("");
        }}
        defaultMode="login"
        showDemoCredentials={!authUser}
        initialEmail={demoEmail}
        initialPassword={demoPassword}
      />
    </div>
  );
}
