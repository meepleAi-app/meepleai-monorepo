'use client';

/**
 * Chat Page Client Component
 *
 * Main chat interface with Zustand store for state management.
 * Extracted from Pages Router for App Router compatibility.
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router
 * Issue #1083: Zustand Chat Store Migration
 */

import { useState } from "react";
import Link from "next/link";
import { useAuth } from "@/components/auth/AuthProvider";
import { ChatStoreProvider } from "@/store/chat/ChatStoreProvider";
import { ChatSidebar } from "@/components/chat/ChatSidebar";
import { ChatContent } from "@/components/chat/ChatContent";
import { BottomNav } from "@/components/chat/BottomNav";
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

  // Main chat interface with Zustand store (Issue #1083)
  return (
    <ChatStoreProvider>
      <main id="main-content" className="flex h-dvh font-sans overflow-hidden">
        <ChatSidebar />
        <ChatContent />
        <BottomNav />

        <ExportChatModal
          isOpen={showExportModal}
          onClose={() => setShowExportModal(false)}
          chatId={"temp-placeholder"}
          gameName={"placeholder"}
        />
      </main>
    </ChatStoreProvider>
  );
}
