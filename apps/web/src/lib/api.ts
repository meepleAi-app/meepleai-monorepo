const API_BASE = process.env.NEXT_PUBLIC_API_BASE || "http://localhost:8080";
export const api = {
  async get<T>(path: string): Promise<T | null> {
    const res = await fetch(`${API_BASE}${path}`, {
      method: "GET",
      credentials: "include"
    });
    if (res.status === 401) return null;
    if (!res.ok) throw new Error(`API ${path} ${res.status}`);
    return res.json();
  },
  async post<T>(path: string, body?: unknown): Promise<T> {
    const res = await fetch(`${API_BASE}${path}`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body ?? {})
    });
    if (res.status === 401) {
      throw new Error("Unauthorized");
    }
    if (!res.ok) throw new Error(`API ${path} ${res.status}`);
    return res.json();
  }
};
