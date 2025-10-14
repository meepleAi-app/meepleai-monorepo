import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/router";
import { motion, AnimatePresence } from "framer-motion";
import { useInView } from "react-intersection-observer";
import { api } from "../lib/api";

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

export default function Home() {
  const router = useRouter();
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [showAuthModal, setShowAuthModal] = useState(false);
  const [authMode, setAuthMode] = useState<"login" | "register">("login");
  const [errorMessage, setErrorMessage] = useState<string>("");
  const [registerForm, setRegisterForm] = useState({
    email: "",
    password: "",
    displayName: "",
    role: "User"
  });
  const [loginForm, setLoginForm] = useState({
    email: "",
    password: ""
  });

  // Intersection Observer hooks for scroll animations
  const [heroRef, heroInView] = useInView({ triggerOnce: true, threshold: 0.1 });
  const [featuresRef, featuresInView] = useInView({ triggerOnce: true, threshold: 0.1 });
  const [keyFeaturesRef, keyFeaturesInView] = useInView({ triggerOnce: true, threshold: 0.1 });

  useEffect(() => {
    void loadCurrentUser();
  }, []);

  const loadCurrentUser = async () => {
    try {
      const res = await api.get<AuthResponse>("/auth/me");
      if (res) {
        setAuthUser(res.user);
      }
    } catch {
      setAuthUser(null);
    }
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage("");
    try {
      const payload = {
        email: registerForm.email,
        password: registerForm.password,
        displayName: registerForm.displayName || undefined,
        role: registerForm.role
      };
      const res = await api.post<AuthResponse>("/auth/register", payload);
      setAuthUser(res.user);
      setShowAuthModal(false);
      void router.push("/chat");
    } catch (err: any) {
      setErrorMessage(err?.message || "Registrazione non riuscita.");
    }
  };

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage("");
    try {
      const res = await api.post<AuthResponse>("/auth/login", {
        email: loginForm.email,
        password: loginForm.password
      });
      setAuthUser(res.user);
      setShowAuthModal(false);
      void router.push("/chat");
    } catch (err: any) {
      setErrorMessage(err?.message || "Accesso non riuscito.");
    }
  };

  const logout = async () => {
    try {
      await api.post("/auth/logout");
    } catch (err) {
      console.error(err);
    } finally {
      setAuthUser(null);
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
          <nav className="hidden md:flex items-center gap-6">
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
                <button onClick={logout} className="btn-secondary text-sm py-2 px-4">
                  Logout
                </button>
              </>
            ) : (
              <button onClick={() => setShowAuthModal(true)} className="btn-primary">
                Get Started
              </button>
            )}
          </nav>
        </div>
      </header>

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
              <span className="gradient-text">Board Game Rules Assistant</span>
            </h1>
            <p className="text-xl text-slate-400 leading-relaxed">
              Never argue about rules again. Get instant, accurate answers from any game's rulebook with AI-powered semantic search.
            </p>
            <div className="flex flex-wrap gap-4">
              <motion.button
                onClick={() => authUser ? router.push("/chat") : setShowAuthModal(true)}
                className="btn-primary text-lg"
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
              >
                {authUser ? "Go to Chat" : "Get Started Free"}
              </motion.button>
              <motion.a
                href="#features"
                className="btn-secondary text-lg"
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
              >
                See How It Works
              </motion.a>
            </div>
            {!authUser && (
              <p className="text-sm text-slate-500 mt-4">
                💡 Try with demo account: <code className="bg-white/10 px-2 py-1 rounded text-slate-300 font-mono">user@meepleai.dev</code> / <code className="bg-white/10 px-2 py-1 rounded text-slate-300 font-mono">Demo123!</code>
              </p>
            )}
          </motion.div>

          {/* Hero Visual */}
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={heroInView ? { opacity: 1, scale: 1 } : {}}
            transition={{ duration: 0.6, delay: 0.2 }}
            className="hidden md:block"
          >
            <div className="card p-6 shadow-2xl shadow-primary-500/20">
              <div className="space-y-4">
                <motion.div
                  initial={{ opacity: 0, x: 20 }}
                  animate={heroInView ? { opacity: 1, x: 0 } : {}}
                  transition={{ duration: 0.4, delay: 0.4 }}
                  className="flex justify-end"
                >
                  <div className="bg-primary-500 text-white px-4 py-3 rounded-2xl max-w-[80%]">
                    How does en passant work in chess?
                  </div>
                </motion.div>
                <motion.div
                  initial={{ opacity: 0, x: -20 }}
                  animate={heroInView ? { opacity: 1, x: 0 } : {}}
                  transition={{ duration: 0.4, delay: 0.6 }}
                  className="space-y-2"
                >
                  <div className="card p-4">
                    <p className="text-sm">
                      <strong>🤖 MeepleAI:</strong> En passant is a special pawn capture that can only occur immediately after a pawn moves two squares forward from its starting position and lands beside an opponent's pawn...
                    </p>
                    <p className="text-xs text-slate-500 mt-2 italic">
                      📖 Sources: Chess Rules (FIDE) - Page 12
                    </p>
                  </div>
                </motion.div>
              </div>
            </div>
          </motion.div>
        </div>

        {/* Scroll Indicator */}
        <motion.div
          className="absolute bottom-10 left-1/2 transform -translate-x-1/2"
          animate={{ y: [0, 10, 0] }}
          transition={{ duration: 2, repeat: Infinity }}
        >
          <svg className="w-6 h-6 text-slate-500" fill="none" strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" viewBox="0 0 24 24" stroke="currentColor">
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
            <h2 className="text-5xl font-bold mb-4">How It Works</h2>
            <p className="text-xl text-slate-400">Three simple steps to never misunderstand rules again</p>
          </motion.div>

          <div className="grid md:grid-cols-3 gap-8">
            {[
              { icon: "📤", title: "1. Upload", description: "Upload any PDF rulebook. Our AI automatically extracts and indexes the content for lightning-fast search." },
              { icon: "💬", title: "2. Ask", description: "Ask questions in natural language. No need to search through pages—just ask like you're talking to an expert." },
              { icon: "⚡", title: "3. Play", description: "Get instant answers with exact sources. Every answer includes page numbers and rule sections for verification." }
            ].map((feature, index) => (
              <motion.div
                key={index}
                initial={{ opacity: 0, y: 20 }}
                animate={featuresInView ? { opacity: 1, y: 0 } : {}}
                transition={{ duration: 0.6, delay: index * 0.1 }}
                className="card text-center p-8 hover:scale-105 transition-transform"
              >
                <div className="text-6xl mb-4">{feature.icon}</div>
                <h3 className="text-2xl font-semibold mb-3">{feature.title}</h3>
                <p className="text-slate-400 leading-relaxed">{feature.description}</p>
              </motion.div>
            ))}
          </div>
        </div>
      </section>

      {/* Key Features Section */}
      <section ref={keyFeaturesRef} className="py-20 px-6 bg-slate-950">
        <div className="max-w-6xl mx-auto">
          <div className="grid md:grid-cols-2 gap-6">
            {[
              { icon: "🎯", title: "Semantic Search", description: "Advanced AI understands context and meaning, not just keywords. Ask complex questions and get accurate answers." },
              { icon: "📚", title: "Multi-Game Support", description: "Upload rulebooks for chess, complex board games, TCGs, and more. Switch between games seamlessly." },
              { icon: "🔍", title: "Source Citations", description: "Every answer includes exact page numbers and sections. Trust but verify with direct source references." },
              { icon: "⚙️", title: "RuleSpec Editor", description: "Create machine-readable rule specifications. Perfect for game designers and tournament organizers." }
            ].map((feature, index) => (
              <motion.div
                key={index}
                initial={{ opacity: 0, x: index % 2 === 0 ? -20 : 20 }}
                animate={keyFeaturesInView ? { opacity: 1, x: 0 } : {}}
                transition={{ duration: 0.6, delay: index * 0.1 }}
                className="card p-6 hover:border-primary-500/50 transition-colors"
              >
                <h3 className="text-xl font-semibold mb-2 flex items-center gap-2">
                  <span className="text-2xl">{feature.icon}</span>
                  {feature.title}
                </h3>
                <p className="text-slate-400">{feature.description}</p>
              </motion.div>
            ))}
          </div>
        </div>
      </section>

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
          <motion.button
            onClick={() => authUser ? router.push("/chat") : setShowAuthModal(true)}
            className="btn-primary text-lg bg-white text-primary-600 hover:bg-slate-100"
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
          >
            {authUser ? "Start Chatting" : "Get Started Free"}
          </motion.button>
        </motion.div>
      </section>

      {/* Footer */}
      <footer className="py-16 px-6 bg-slate-950 border-t border-white/10">
        <div className="max-w-6xl mx-auto grid md:grid-cols-4 gap-12 mb-12">
          <div className="space-y-4">
            <div className="flex items-center gap-3">
              <span className="text-3xl">🎲</span>
              <span className="text-xl font-bold gradient-text">MeepleAI</span>
            </div>
            <p className="text-slate-400 text-sm">AI-powered board game rules assistance</p>
          </div>

          <div className="space-y-3">
            <h4 className="font-semibold">Product</h4>
            <div className="flex flex-col gap-2 text-sm">
              <Link href="/chat" className="text-slate-400 hover:text-white transition-colors">Chat</Link>
              <Link href="/upload" className="text-slate-400 hover:text-white transition-colors">Upload PDF</Link>
              <Link href="/editor" className="text-slate-400 hover:text-white transition-colors">RuleSpec Editor</Link>
            </div>
          </div>

          <div className="space-y-3">
            <h4 className="font-semibold">Resources</h4>
            <div className="flex flex-col gap-2 text-sm">
              <a href="https://github.com/yourusername/meepleai" target="_blank" rel="noopener noreferrer" className="text-slate-400 hover:text-white transition-colors">GitHub</a>
              <Link href="/docs" className="text-slate-400 hover:text-white transition-colors">Documentation</Link>
              <Link href="/logs" className="text-slate-400 hover:text-white transition-colors">API Logs</Link>
            </div>
          </div>

          <div className="space-y-3">
            <h4 className="font-semibold">Demo Accounts</h4>
            <div className="text-sm text-slate-400 space-y-1">
              <p>admin@meepleai.dev</p>
              <p>editor@meepleai.dev</p>
              <p>user@meepleai.dev</p>
              <p className="font-mono">Password: Demo123!</p>
            </div>
          </div>
        </div>

        <div className="max-w-6xl mx-auto pt-8 border-t border-white/10 text-center text-sm text-slate-500">
          <p>© 2025 MeepleAI. Open source project.</p>
        </div>
      </footer>

      {/* Auth Modal */}
      <AnimatePresence>
        {showAuthModal && (
          <>
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              onClick={() => setShowAuthModal(false)}
              className="fixed inset-0 bg-black/70 backdrop-blur-sm z-50 flex items-center justify-center p-4"
            >
              <motion.div
                initial={{ opacity: 0, scale: 0.95 }}
                animate={{ opacity: 1, scale: 1 }}
                exit={{ opacity: 0, scale: 0.95 }}
                onClick={(e) => e.stopPropagation()}
                className="bg-slate-900 border border-white/10 rounded-2xl p-8 max-w-md w-full relative"
              >
                <button
                  onClick={() => setShowAuthModal(false)}
                  className="absolute top-4 right-4 text-slate-400 hover:text-white transition-colors text-2xl"
                >
                  ✕
                </button>

                <div className="flex gap-2 mb-6 border-b border-white/10">
                  <button
                    onClick={() => setAuthMode("login")}
                    className={`px-6 py-3 font-medium transition-all ${
                      authMode === "login"
                        ? "text-primary-500 border-b-2 border-primary-500"
                        : "text-slate-400 hover:text-white"
                    }`}
                  >
                    Login
                  </button>
                  <button
                    onClick={() => setAuthMode("register")}
                    className={`px-6 py-3 font-medium transition-all ${
                      authMode === "register"
                        ? "text-primary-500 border-b-2 border-primary-500"
                        : "text-slate-400 hover:text-white"
                    }`}
                  >
                    Register
                  </button>
                </div>

                {errorMessage && (
                  <div className="bg-red-500/10 border border-red-500/30 text-red-400 px-4 py-3 rounded-lg mb-4">
                    {errorMessage}
                  </div>
                )}

                {authMode === "login" ? (
                  <form onSubmit={handleLogin} className="space-y-4">
                    <div className="space-y-2">
                      <label className="text-sm font-medium text-slate-300">Email</label>
                      <input
                        type="email"
                        value={loginForm.email}
                        onChange={(e) => setLoginForm({ ...loginForm, email: e.target.value })}
                        className="w-full bg-white/5 border border-white/10 rounded-lg px-4 py-3 text-white outline-none focus:border-primary-500 transition-colors"
                        required
                      />
                    </div>
                    <div className="space-y-2">
                      <label className="text-sm font-medium text-slate-300">Password</label>
                      <input
                        type="password"
                        value={loginForm.password}
                        onChange={(e) => setLoginForm({ ...loginForm, password: e.target.value })}
                        className="w-full bg-white/5 border border-white/10 rounded-lg px-4 py-3 text-white outline-none focus:border-primary-500 transition-colors"
                        required
                      />
                    </div>
                    <button type="submit" className="w-full btn-primary mt-6">
                      Login
                    </button>
                  </form>
                ) : (
                  <form onSubmit={handleRegister} className="space-y-4">
                    <div className="space-y-2">
                      <label className="text-sm font-medium text-slate-300">Email</label>
                      <input
                        type="email"
                        value={registerForm.email}
                        onChange={(e) => setRegisterForm({ ...registerForm, email: e.target.value })}
                        className="w-full bg-white/5 border border-white/10 rounded-lg px-4 py-3 text-white outline-none focus:border-primary-500 transition-colors"
                        required
                      />
                    </div>
                    <div className="space-y-2">
                      <label className="text-sm font-medium text-slate-300">Password (min 8 characters)</label>
                      <input
                        type="password"
                        value={registerForm.password}
                        onChange={(e) => setRegisterForm({ ...registerForm, password: e.target.value })}
                        className="w-full bg-white/5 border border-white/10 rounded-lg px-4 py-3 text-white outline-none focus:border-primary-500 transition-colors"
                        required
                        minLength={8}
                      />
                    </div>
                    <div className="space-y-2">
                      <label className="text-sm font-medium text-slate-300">Display Name (optional)</label>
                      <input
                        value={registerForm.displayName}
                        onChange={(e) => setRegisterForm({ ...registerForm, displayName: e.target.value })}
                        className="w-full bg-white/5 border border-white/10 rounded-lg px-4 py-3 text-white outline-none focus:border-primary-500 transition-colors"
                      />
                    </div>
                    <div className="space-y-2">
                      <label className="text-sm font-medium text-slate-300">Role</label>
                      <select
                        value={registerForm.role}
                        onChange={(e) => setRegisterForm({ ...registerForm, role: e.target.value })}
                        className="w-full bg-white/5 border border-white/10 rounded-lg px-4 py-3 text-white outline-none focus:border-primary-500 transition-colors"
                      >
                        <option value="User">User</option>
                        <option value="Editor">Editor</option>
                        <option value="Admin">Admin</option>
                      </select>
                    </div>
                    <button type="submit" className="w-full btn-primary mt-6">
                      Create Account
                    </button>
                  </form>
                )}
              </motion.div>
            </motion.div>
          </>
        )}
      </AnimatePresence>
    </div>
  );
}
