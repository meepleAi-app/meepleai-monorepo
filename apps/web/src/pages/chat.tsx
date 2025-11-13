import { useState } from "react";
import Link from "next/link";
import { useAuth } from "@/components/auth/AuthProvider";
import { GameProvider } from "@/components/game/GameProvider";
import { ChatProvider } from "@/components/chat/ChatProvider";
import { UIProvider } from "@/components/ui/UIProvider";
import { ChatSidebar } from "@/components/chat/ChatSidebar";
import { ChatContent } from "@/components/chat/ChatContent";
import { ExportChatModal } from "@/components/ExportChatModal";

export default function ChatPage() {
  const { user: authUser, loading } = useAuth();
  const [showExportModal, setShowExportModal] = useState(false);

  // Show loading state while checking auth
  if (loading) {
    return (
      <main id="main-content" style={{ padding: 24, maxWidth: 900, margin: "0 auto", fontFamily: "sans-serif" }}>
        <div style={{ textAlign: "center", marginTop: 48 }}>
          <p>Loading...</p>
        </div>
      </main>
    );
  }

  // Render login required state
  if (!authUser) {
    return (
      <main id="main-content" style={{ padding: 24, maxWidth: 900, margin: "0 auto", fontFamily: "sans-serif" }}>
        <Link href="/" style={{ color: "#3391ff", textDecoration: "none" }}>
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

  // Main chat interface with new provider hierarchy
  return (
    <GameProvider>
      <ChatProvider>
        <UIProvider>
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
        </UIProvider>
      </ChatProvider>
    </GameProvider>
  );
}
