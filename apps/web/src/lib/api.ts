const API_BASE = process.env.NEXT_PUBLIC_API_BASE || "http://localhost:8080";
export const api = {
  async post(path: string, body: unknown) {
    const res = await fetch(`${API_BASE}${path}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    if (!res.ok) throw new Error(`API ${path} ${res.status}`);
    return res.json();
  }
};
