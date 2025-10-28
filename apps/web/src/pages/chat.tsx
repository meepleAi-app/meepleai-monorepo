import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "../lib/api";
import { ChatProvider } from "@/components/chat/ChatProvider";
import { ChatSidebar } from "@/components/chat/ChatSidebar";
import { ChatContent } from "@/components/chat/ChatContent";
import { ExportChatModal } from "@/components/ExportChatModal";

// Type definitions
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

type Game = {
  id: string;
  name: string;
};

export default function ChatPage() {
  // Authentication
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [showExportModal, setShowExportModal] = useState(false);

  // Load current user on mount
  useEffect(() => {
    void loadCurrentUser();
  }, []);

  const loadCurrentUser = async () => {
    try {
      const res = await api.get<AuthResponse>("/api/v1/auth/me");
      if (res) {
        setAuthUser(res.user);
      } else {
        setAuthUser(null);
      }
    } catch {
      setAuthUser(null);
    }
  };

  // Render login required state
  if (!authUser) {
    return (
      <main id="main-content" style={{ padding: 24, maxWidth: 900, margin: "0 auto", fontFamily: "sans-serif" }}>
        <Link href="/" style={{ color: "#0070f3", textDecoration: "none" }}>
          ← Torna alla Home
        </Link>
        <div
          style={{
            marginTop: 24,
            padding: 32,
            textAlign: "center",
            border: "1px solid #dadce0",
            borderRadius: 8
          }}
        >
          <h2>Accesso richiesto</h2>
          <p>Devi effettuare l&apos;accesso per utilizzare la chat.</p>
          <Link
            href="/"
            style={{
              display: "inline-block",
              marginTop: 16,
              padding: "8px 16px",
              background: "#0070f3",
              color: "white",
              textDecoration: "none",
              borderRadius: 4
            }}
          >
            Vai al Login
          </Link>
        </div>
      </main>
    );
  }

  // Main chat interface with new component architecture
  return (
    <ChatProvider>
      <main
        id="main-content"
        style={{
          display: "flex",
          height: "100vh",
          fontFamily: "sans-serif",
          overflow: "hidden"
        }}
      >
        <ChatSidebar />
        <ChatContent />

        {/* Export Chat Modal - will be integrated into ChatContent in future */}
        <ExportChatModal
          isOpen={showExportModal}
          onClose={() => setShowExportModal(false)}
          chatId={"temp-placeholder"}
          gameName={"placeholder"}
        />
      </main>
    </ChatProvider>
  );
}
