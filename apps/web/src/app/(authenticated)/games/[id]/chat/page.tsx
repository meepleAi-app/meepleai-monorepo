// TODO: implement this route (see docs/superpowers/specs/route-gap-analysis.md)
// Mockup: sp4-game-chat-tab — AI chat tab embedded in the game detail view.
// Redirects to the parent /games/[id] until chat is implemented as a sub-tab.
"use client";

import { useEffect } from "react";
import { useRouter, useParams } from "next/navigation";

export default function GameChatPlaceholderPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();

  useEffect(() => {
    const timer = setTimeout(() => {
      router.replace(`/games/${params.id}`);
    }, 2000);
    return () => clearTimeout(timer);
  }, [router, params.id]);

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4 bg-background p-8">
      <h1 className="text-2xl font-semibold text-foreground">Coming Soon</h1>
      <p className="text-muted-foreground">
        This page is not yet implemented. Redirecting to <code>/games/{params.id}</code>...
      </p>
    </main>
  );
}
