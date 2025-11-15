import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/router";
import { motion } from "framer-motion";
import { useInView } from "react-intersection-observer";
import { api } from "../lib/api";
import { AccessibleButton } from "@/components/accessible";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { LoadingButton } from "@/components/loading/LoadingButton";

type AuthUser = {
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
};

type AuthResponse = {
  user: AuthUser;
  expiresAt: string;
};

export default function BoardGameAI() {
  const router = useRouter();
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  // Intersection Observer hooks for scroll animations
  const [heroRef, heroInView] = useInView({ triggerOnce: true, threshold: 0.1 });
  const [featuresRef, featuresInView] = useInView({ triggerOnce: true, threshold: 0.1 });
  const [howItWorksRef, howItWorksInView] = useInView({ triggerOnce: true, threshold: 0.1 });
  const [ctaRef, ctaInView] = useInView({ triggerOnce: true, threshold: 0.1 });

  useEffect(() => {
    void loadCurrentUser();
  }, []);

  const loadCurrentUser = async () => {
    try {
      const res = await api.get<AuthResponse>("/api/v1/auth/me");
      if (res) {
        setAuthUser(res.user);
      }
    } catch {
      setAuthUser(null);
    } finally {
      setLoading(false);
    }
  };

  const logout = async () => {
    try {
      await api.post("/api/v1/auth/logout");
    } catch (err) {
      console.error(err);
    } finally {
      setAuthUser(null);
    }
  };

  const handleGetStarted = () => {
    if (authUser) {
      void router.push("/board-game-ai/ask");
    } else {
      void router.push("/login?redirect=/board-game-ai/ask");
    }
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
          <nav aria-label="Main navigation" className="hidden md:flex items-center gap-6">
            <Link href="/board-game-ai" className="text-white font-semibold">
              Board Game AI
            </Link>
            {authUser ? (
              <>
                <Link href="/chat" className="text-slate-300 hover:text-white transition-colors">
                  Chat
                </Link>
                <Link href="/upload" className="text-slate-300 hover:text-white transition-colors">
                  Upload
                </Link>
                {authUser.role === "Admin" && (
                  <Link href="/admin" className="text-slate-300 hover:text-white transition-colors">
                    Admin
                  </Link>
                )}
                <Button
                  onClick={logout}
                  variant="outline"
                  className="text-sm py-2 px-4"
                  aria-label="Logout from MeepleAI"
                >
                  Logout
                </Button>
              </>
            ) : (
              <Button asChild>
                <Link href="/login">
                  Login
                </Link>
              </Button>
            )}
          </nav>
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
                AI-Powered
                <br />
                <span className="gradient-text-accessible">Board Game Rules</span>
                <br />
                Expert
              </h1>
              <p className="text-xl text-slate-50 leading-relaxed">
                Get instant, accurate answers to any board game rule question. Our AI understands complex rule interactions and provides precise citations from official rulebooks.
              </p>
              <div className="flex flex-wrap gap-4">
                <LoadingButton
                  onClick={handleGetStarted}
                  isLoading={loading}
                  loadingText="Loading..."
                  className="text-lg"
                  data-testid="hero-get-started"
                >
                  {authUser ? "Ask a Question" : "Get Started Free"}
                </LoadingButton>
                <Button
                  asChild
                  variant="outline"
                  className="text-lg"
                >
                  <motion.a
                    href="#features"
                    whileHover={{ scale: 1.05 }}
                    whileTap={{ scale: 0.95 }}
                  >
                    Learn More
                  </motion.a>
                </Button>
              </div>
            </motion.div>

            {/* Hero Visual - Example Q&A */}
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={heroInView ? { opacity: 1, scale: 1 } : {}}
              transition={{ duration: 0.6, delay: 0.2 }}
              className="hidden md:block"
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
                      In Terraforming Mars, can I use a Standard Project during the production phase?
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
                        <strong>🤖 Board Game AI:</strong> No, Standard Projects can only be performed during the Action Phase, not during the Production Phase. The Production Phase is exclusively for generating resources and moving the production track markers.
                      </p>
                      <p className="text-xs text-slate-300 mt-2 italic">
                        📖 Sources: Terraforming Mars Rulebook - Page 5, Section "Game Phases"
                      </p>
                      <div className="mt-2 flex items-center gap-2 text-xs">
                        <span className="bg-green-500/20 text-green-400 px-2 py-1 rounded">Confidence: 95%</span>
                      </div>
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

        {/* Features Section */}
        <section id="features" ref={featuresRef} className="py-20 px-6 bg-gradient-to-b from-slate-950 to-slate-900">
          <div className="max-w-6xl mx-auto">
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={featuresInView ? { opacity: 1, y: 0 } : {}}
              transition={{ duration: 0.6 }}
              className="text-center mb-16"
            >
              <h2 className="text-5xl font-bold mb-4">Why Board Game AI?</h2>
              <p className="text-xl text-slate-50">Advanced AI trained specifically for board game rules</p>
            </motion.div>

            <div className="grid md:grid-cols-2 gap-6">
              {[
                {
                  icon: "🎯",
                  title: "Multi-Model Consensus",
                  description: "We use multiple AI models (GPT-4, Claude) to validate answers, ensuring 95%+ accuracy on complex rule interactions."
                },
                {
                  icon: "📚",
                  title: "100+ Games Supported",
                  description: "From Catan to Terraforming Mars, Wingspan to Scythe. We support popular modern board games with official rulebook integration."
                },
                {
                  icon: "🔍",
                  title: "Precise Citations",
                  description: "Every answer includes exact page numbers and rule sections. Click to jump directly to the relevant section in the PDF."
                },
                {
                  icon: "⚡",
                  title: "Lightning Fast",
                  description: "Hybrid search combining vector similarity and keyword matching delivers answers in under 3 seconds."
                },
                {
                  icon: "🌐",
                  title: "Multilingual Support",
                  description: "Ask questions in Italian or English. Our AI understands context in multiple languages."
                },
                {
                  icon: "🛡️",
                  title: "5-Layer Validation",
                  description: "Confidence scoring, citation verification, multi-model consensus, and forbidden keyword detection ensure quality."
                }
              ].map((feature, index) => (
                <Card key={index}>
                  <motion.div
                    initial={{ opacity: 0, x: index % 2 === 0 ? -20 : 20 }}
                    animate={featuresInView ? { opacity: 1, x: 0 } : {}}
                    transition={{ duration: 0.6, delay: index * 0.1 }}
                    className="p-6 hover:border-primary/50 transition-colors"
                  >
                    <h3 className="text-xl font-semibold mb-2 flex items-center gap-2">
                      <span className="text-2xl">{feature.icon}</span>
                      {feature.title}
                    </h3>
                    <p className="text-slate-50">{feature.description}</p>
                  </motion.div>
                </Card>
              ))}
            </div>
          </div>
        </section>

        {/* How It Works Section */}
        <section ref={howItWorksRef} className="py-20 px-6 bg-slate-950">
          <div className="max-w-6xl mx-auto">
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={howItWorksInView ? { opacity: 1, y: 0 } : {}}
              transition={{ duration: 0.6 }}
              className="text-center mb-16"
            >
              <h2 className="text-5xl font-bold mb-4">How It Works</h2>
              <p className="text-xl text-slate-50">Get accurate answers in three simple steps</p>
            </motion.div>

            <div className="grid md:grid-cols-3 gap-8">
              {[
                {
                  icon: "1️⃣",
                  title: "Select Your Game",
                  description: "Choose from our library of 100+ board games, or upload your own PDF rulebook."
                },
                {
                  icon: "2️⃣",
                  title: "Ask Your Question",
                  description: "Type your rule question in natural language. No need for exact keywords or search terms."
                },
                {
                  icon: "3️⃣",
                  title: "Get Verified Answer",
                  description: "Receive an AI-generated answer with confidence score, citations, and the option to view the source PDF."
                }
              ].map((step, index) => (
                <Card key={index}>
                  <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    animate={howItWorksInView ? { opacity: 1, y: 0 } : {}}
                    transition={{ duration: 0.6, delay: index * 0.1 }}
                    className="text-center p-8 hover:scale-105 transition-transform"
                  >
                    <div className="text-6xl mb-4">{step.icon}</div>
                    <h3 className="text-2xl font-semibold mb-3">{step.title}</h3>
                    <p className="text-slate-50 leading-relaxed">{step.description}</p>
                  </motion.div>
                </Card>
              ))}
            </div>
          </div>
        </section>

        {/* CTA Section */}
        <section ref={ctaRef} className="py-20 px-6 bg-gradient-cta">
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={ctaInView ? { opacity: 1, scale: 1 } : {}}
            transition={{ duration: 0.6 }}
            className="max-w-4xl mx-auto text-center space-y-6"
          >
            <h2 className="text-5xl font-bold">Ready to Resolve Your Rules Disputes?</h2>
            <p className="text-xl opacity-90">
              Join thousands of board game enthusiasts who trust AI for accurate rule clarifications
            </p>
            <LoadingButton
              onClick={handleGetStarted}
              isLoading={loading}
              loadingText="Loading..."
              className="text-lg bg-white text-primary hover:bg-slate-100"
              data-testid="cta-get-started"
            >
              {authUser ? "Ask Your First Question" : "Get Started Free"}
            </LoadingButton>
            <p className="text-sm text-slate-50 mt-4">
              💡 No credit card required • 95%+ accuracy • Instant answers
            </p>
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
              <Link href="/board-game-ai" className="text-slate-300 hover:text-white transition-colors">Board Game AI</Link>
              <Link href="/board-game-ai/games" className="text-slate-300 hover:text-white transition-colors">Game Catalog</Link>
              <Link href="/upload" className="text-slate-300 hover:text-white transition-colors">Upload PDF</Link>
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
            <h4 className="font-semibold">Support</h4>
            <div className="flex flex-col gap-2 text-sm">
              <Link href="/chat" className="text-slate-300 hover:text-white transition-colors">General Chat</Link>
              <Link href="/profile" className="text-slate-300 hover:text-white transition-colors">Profile</Link>
              <Link href="/settings" className="text-slate-300 hover:text-white transition-colors">Settings</Link>
            </div>
          </div>
        </div>

        <div className="max-w-6xl mx-auto pt-8 border-t border-white/10 text-center text-sm text-slate-300">
          <p>© 2025 MeepleAI. Open source board game rules assistant.</p>
        </div>
      </footer>
    </div>
  );
}
