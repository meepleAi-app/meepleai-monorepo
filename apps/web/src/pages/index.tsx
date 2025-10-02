import { FormEvent, useEffect, useState } from "react";
import Link from "next/link";
import { api } from "../lib/api";

type AuthUser = {
  id: string;
  tenantId: string;
  email: string;
  displayName?: string | null;
  role: string;
};

type AuthResponse = {
  user: AuthUser;
  expiresAt: string;
};

const DEFAULT_TENANT = process.env.NEXT_PUBLIC_TENANT_ID || "dev";

export default function Home() {
  const [answer, setAnswer] = useState<string>("");
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [statusMessage, setStatusMessage] = useState<string>("");
  const [errorMessage, setErrorMessage] = useState<string>("");
  const [registerForm, setRegisterForm] = useState({
    tenantId: DEFAULT_TENANT,
    tenantName: "",
    email: "",
    password: "",
    displayName: "",
    role: "User"
  });
  const [loginForm, setLoginForm] = useState({
    tenantId: DEFAULT_TENANT,
    email: "",
    password: ""
  });
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    void loadCurrentUser();
  }, []);

  const loadCurrentUser = async () => {
    try {
      const res = await api.get<AuthResponse>("/auth/me");
      if (res) {
        setAuthUser(res.user);
      } else {
        setAuthUser(null);
      }
    } catch {
      setAuthUser(null);
    }
  };

  const ping = async () => {
    const res = await fetch("/api/health");
    const j = await res.json();
    alert(`Web OK: ${j.ok}`);
  };

  const ask = async () => {
    if (!authUser) {
      setErrorMessage("Devi effettuare l'accesso per porre domande.");
      return;
    }
    setErrorMessage("");
    setIsLoading(true);
    try {
      const res = await api.post<{ answer: string }>("/agents/qa", {
        tenantId: authUser.tenantId,
        gameId: "demo-chess",
        query: "How many players?"
      });
      setAnswer(res.answer);
      setStatusMessage("Risposta aggiornata.");
    } catch (err) {
      console.error(err);
      setErrorMessage("Impossibile interrogare l'agente. Controlla le credenziali.");
    } finally {
      setIsLoading(false);
    }
  };

  const handleRegister = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setErrorMessage("");
    try {
      const payload = {
        tenantId: registerForm.tenantId,
        tenantName: registerForm.tenantName || undefined,
        email: registerForm.email,
        password: registerForm.password,
        displayName: registerForm.displayName || undefined,
        role: registerForm.role
      };
      const res = await api.post<AuthResponse>("/auth/register", payload);
      setAuthUser(res.user);
      setStatusMessage("Registrazione completata. Sei connesso!");
      setRegisterForm({
        tenantId: registerForm.tenantId,
        tenantName: "",
        email: "",
        password: "",
        displayName: "",
        role: "User"
      });
    } catch (err: any) {
      console.error(err);
      setErrorMessage(err?.message || "Registrazione non riuscita.");
    }
  };

  const handleLogin = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setErrorMessage("");
    try {
      const res = await api.post<AuthResponse>("/auth/login", {
        tenantId: loginForm.tenantId,
        email: loginForm.email,
        password: loginForm.password
      });
      setAuthUser(res.user);
      setStatusMessage("Accesso eseguito.");
      setLoginForm({ tenantId: loginForm.tenantId, email: "", password: "" });
    } catch (err: any) {
      console.error(err);
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
      setStatusMessage("Sessione terminata.");
    }
  };

  return (
    <main style={{ padding: 24, maxWidth: 800, margin: "0 auto", fontFamily: "sans-serif" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <div>
          <h1>MeepleAI</h1>
          <p>Frontend ↔ MeepleAgentAI</p>
        </div>
        <div style={{ display: "flex", gap: 12 }}>
          <Link
            href="/chat"
            style={{
              padding: "8px 16px",
              background: "#34a853",
              color: "white",
              textDecoration: "none",
              borderRadius: 4,
              fontSize: 14
            }}
          >
            Chat
          </Link>
          <Link
            href="/upload"
            style={{
              padding: "8px 16px",
              background: "#0070f3",
              color: "white",
              textDecoration: "none",
              borderRadius: 4,
              fontSize: 14
            }}
          >
            Upload PDF
          </Link>
          <Link
            href="/logs"
            style={{
              padding: "8px 16px",
              background: "#1a73e8",
              color: "white",
              textDecoration: "none",
              borderRadius: 4,
              fontSize: 14
            }}
          >
            View Logs
          </Link>
          <Link
            href="/editor?gameId=demo-chess"
            style={{
              padding: "8px 16px",
              background: "#ea4335",
              color: "white",
              textDecoration: "none",
              borderRadius: 4,
              fontSize: 14
            }}
          >
            Editor RuleSpec
          </Link>
        </div>
      </div>

      {statusMessage && <p style={{ color: "#0070f3" }}>{statusMessage}</p>}
      {errorMessage && <p style={{ color: "#d93025" }}>{errorMessage}</p>}

      <section style={{ display: "flex", gap: 24, flexWrap: "wrap" }}>
        <form onSubmit={handleRegister} style={{ flex: "1 1 320px", border: "1px solid #ccc", padding: 16, borderRadius: 8 }}>
          <h2>Registrazione</h2>
          <label style={{ display: "block", marginBottom: 8 }}>
            Tenant ID
            <input
              value={registerForm.tenantId}
              onChange={(e) => setRegisterForm({ ...registerForm, tenantId: e.target.value })}
              style={{ width: "100%" }}
              required
            />
          </label>
          <label style={{ display: "block", marginBottom: 8 }}>
            Nome Tenant
            <input
              value={registerForm.tenantName}
              onChange={(e) => setRegisterForm({ ...registerForm, tenantName: e.target.value })}
              style={{ width: "100%" }}
            />
          </label>
          <label style={{ display: "block", marginBottom: 8 }}>
            Email
            <input
              type="email"
              value={registerForm.email}
              onChange={(e) => setRegisterForm({ ...registerForm, email: e.target.value })}
              style={{ width: "100%" }}
              required
            />
          </label>
          <label style={{ display: "block", marginBottom: 8 }}>
            Password (min 8 caratteri)
            <input
              type="password"
              value={registerForm.password}
              onChange={(e) => setRegisterForm({ ...registerForm, password: e.target.value })}
              style={{ width: "100%" }}
              required
              minLength={8}
            />
          </label>
          <label style={{ display: "block", marginBottom: 8 }}>
            Nome visualizzato
            <input
              value={registerForm.displayName}
              onChange={(e) => setRegisterForm({ ...registerForm, displayName: e.target.value })}
              style={{ width: "100%" }}
            />
          </label>
          <label style={{ display: "block", marginBottom: 12 }}>
            Ruolo
            <select
              value={registerForm.role}
              onChange={(e) => setRegisterForm({ ...registerForm, role: e.target.value })}
              style={{ width: "100%" }}
            >
              <option value="Admin">Admin</option>
              <option value="Editor">Editor</option>
              <option value="User">User</option>
            </select>
          </label>
          <button type="submit">Crea account</button>
        </form>

        <form onSubmit={handleLogin} style={{ flex: "1 1 320px", border: "1px solid #ccc", padding: 16, borderRadius: 8 }}>
          <h2>Accesso</h2>
          <label style={{ display: "block", marginBottom: 8 }}>
            Tenant ID
            <input
              value={loginForm.tenantId}
              onChange={(e) => setLoginForm({ ...loginForm, tenantId: e.target.value })}
              style={{ width: "100%" }}
              required
            />
          </label>
          <label style={{ display: "block", marginBottom: 8 }}>
            Email
            <input
              type="email"
              value={loginForm.email}
              onChange={(e) => setLoginForm({ ...loginForm, email: e.target.value })}
              style={{ width: "100%" }}
              required
            />
          </label>
          <label style={{ display: "block", marginBottom: 12 }}>
            Password
            <input
              type="password"
              value={loginForm.password}
              onChange={(e) => setLoginForm({ ...loginForm, password: e.target.value })}
              style={{ width: "100%" }}
              required
            />
          </label>
          <button type="submit">Entra</button>
          <button type="button" onClick={logout} style={{ marginLeft: 8 }}>
            Esci
          </button>
        </form>
      </section>

      <section style={{ marginTop: 32 }}>
        <h2>Sessione corrente</h2>
        {authUser ? (
          <div style={{ border: "1px solid #ccc", padding: 16, borderRadius: 8 }}>
            <p>
              <strong>Email:</strong> {authUser.email}
            </p>
            <p>
              <strong>Tenant:</strong> {authUser.tenantId}
            </p>
            <p>
              <strong>Ruolo:</strong> {authUser.role}
            </p>
            {authUser.displayName && (
              <p>
                <strong>Nome:</strong> {authUser.displayName}
              </p>
            )}
          </div>
        ) : (
          <p>Nessun utente connesso.</p>
        )}
      </section>

      <section style={{ marginTop: 32 }}>
        <h2>Agente QA Demo</h2>
        <button onClick={ping}>Ping Web</button>
        <button onClick={ask} style={{ marginLeft: 12 }} disabled={!authUser || isLoading}>
          {isLoading ? "Richiesta..." : "Chiedi"}
        </button>
        <pre style={{ background: "#f5f5f5", padding: 12, marginTop: 16 }}>{answer}</pre>
      </section>
    </main>
  );
}
