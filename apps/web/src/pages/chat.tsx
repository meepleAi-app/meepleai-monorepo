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
      <main id="main-content" className="p-6 max-w-4xl mx-auto font-sans">
        <div className="text-center mt-12">
          <p>Loading...</p>
        </div>
      </main>
    );
  }

  // Render login required state
  if (!authUser) {
    return (
      <main id="main-content" className="p-6 max-w-4xl mx-auto font-sans">
        <Link href="/" className="text-[#3391ff] no-underline">
          ← Torna alla Home
        </Link>
        <div className="mt-6 p-8 text-center border border-[#dadce0] rounded-lg">
          <h2>Accesso richiesto</h2>
          <p>Devi effettuare l&apos;accesso per utilizzare la chat.</p>
          <Link
            href="/"
            className="inline-block mt-4 px-4 py-2 bg-[#0070f3] text-white no-underline rounded"
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
          <main id="main-content" className="flex h-screen font-sans overflow-hidden">
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
