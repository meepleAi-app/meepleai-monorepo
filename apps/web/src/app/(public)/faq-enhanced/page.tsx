// TODO: implement this route (see docs/superpowers/specs/route-gap-analysis.md)
// Mockup: sp3-faq-enhanced.jsx — functionality already implemented at /faq.
// This placeholder exists so nav-map.md routes resolve; redirects to /faq.
"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

export default function FaqEnhancedPlaceholderPage() {
  const router = useRouter();

  useEffect(() => {
    const timer = setTimeout(() => {
      router.replace("/faq");
    }, 2000);
    return () => clearTimeout(timer);
  }, [router]);

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4 bg-background p-8">
      <h1 className="text-2xl font-semibold text-foreground">Coming Soon</h1>
      <p className="text-muted-foreground">
        This page is not yet implemented. Redirecting to <code>/faq</code>...
      </p>
    </main>
  );
}
