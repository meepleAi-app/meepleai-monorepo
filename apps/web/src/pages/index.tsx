import { useState } from "react";
import { api } from "../lib/api";

export default function Home() {
  const [answer, setAnswer] = useState<string>("");

  const ping = async () => {
    const res = await fetch("/api/health");
    const j = await res.json();
    alert(`Web OK: ${j.ok}`);
  };

  const ask = async () => {
    const res = await api.post("/agents/qa", {
      tenantId: process.env.NEXT_PUBLIC_TENANT_ID,
      gameId: "demo-chess",
      query: "How many players?"
    });
    setAnswer(res.answer);
  };

  return (
    <main style={{ padding: 24 }}>
      <h1>MeepleAI</h1>
      <p>Frontend ↔ MeepleAgentAI</p>
      <button onClick={ping}>Ping Web</button>
      <button onClick={ask} style={{ marginLeft: 12 }}>Ask QA</button>
      <pre>{answer}</pre>
    </main>
  );
}
