export const API_BASE_FALLBACK = "http://localhost:8080";

export const getApiBase = (): string => {
  const envBase = process.env.NEXT_PUBLIC_API_BASE?.trim();
  if (envBase && envBase !== "undefined" && envBase !== "null") {
    return envBase;
  }
  return API_BASE_FALLBACK;
};

export const api = {
  async get<T>(path: string): Promise<T | null> {
    const res = await fetch(`${getApiBase()}${path}`, {
      method: "GET",
      credentials: "include"
    });
    if (res.status === 401) {
      return null;
    }
    if (!res.ok) {
      throw new Error(`API ${path} ${res.status}`);
    }
    return res.json();
  },
  async post<T>(path: string, body?: unknown): Promise<T> {
    const res = await fetch(`${getApiBase()}${path}`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body ?? {})
    });
    if (res.status === 401) {
      throw new Error("Unauthorized");
    }
    if (!res.ok) {
      throw new Error(`API ${path} ${res.status}`);
    }
    return res.json();
  },
  async put<T>(path: string, body: unknown): Promise<T> {
    const res = await fetch(`${getApiBase()}${path}`, {
      method: "PUT",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    if (res.status === 401) {
      throw new Error("Unauthorized");
    }
    if (!res.ok) {
      throw new Error(`API ${path} ${res.status}`);
    }
    return res.json();
  },
  async delete(path: string): Promise<void> {
    const res = await fetch(`${getApiBase()}${path}`, {
      method: "DELETE",
      credentials: "include"
    });
    if (res.status === 401) {
      throw new Error("Unauthorized");
    }
    if (!res.ok) {
      throw new Error(`API ${path} ${res.status}`);
    }
    // DELETE returns 204 NoContent, no body to parse
  }
};
