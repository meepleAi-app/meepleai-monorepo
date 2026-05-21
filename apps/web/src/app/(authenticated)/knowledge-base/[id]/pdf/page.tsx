// TODO: implement this route (see docs/superpowers/specs/route-gap-analysis.md)
// Mockup: sp4-citation-pdf-viewer — inline PDF viewer for KB documents.
// Redirects to the parent /knowledge-base/[id] until PDF viewer is built.
"use client";

import { useEffect } from "react";
import { useRouter, useParams } from "next/navigation";

export default function KbPdfPlaceholderPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();

  useEffect(() => {
    const timer = setTimeout(() => {
      router.replace(`/knowledge-base/${params.id}`);
    }, 2000);
    return () => clearTimeout(timer);
  }, [router, params.id]);

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4 bg-background p-8">
      <h1 className="text-2xl font-semibold text-foreground">Coming Soon</h1>
      <p className="text-muted-foreground">
        This page is not yet implemented. Redirecting to{" "}
        <code>/knowledge-base/{params.id}</code>...
      </p>
    </main>
  );
}
