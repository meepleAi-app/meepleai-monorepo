import { api } from "../api";

describe("api", () => {
  let originalFetch: typeof global.fetch;
  let fetchMock: jest.MockedFunction<typeof fetch>;

  const setFetchResponse = (status: number, payload?: unknown) => {
    fetchMock.mockResolvedValue({
      ok: status >= 200 && status < 300,
      status,
      json: async () => payload
    } as Response);
  };

  beforeEach(() => {
    originalFetch = global.fetch;
    fetchMock = jest.fn() as jest.MockedFunction<typeof fetch>;
    global.fetch = fetchMock;
  });

  afterEach(() => {
    global.fetch = originalFetch;
    fetchMock.mockReset();
  });

  it("returns parsed JSON for successful get", async () => {
    const data = { id: "game" };
    setFetchResponse(200, data);

    const result = await api.get<typeof data>("/games");

    expect(result).toEqual(data);
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:8080/games", {
      method: "GET",
      credentials: "include"
    });
  });

  it("returns null for 401 get", async () => {
    setFetchResponse(401);

    const result = await api.get("/games");

    expect(result).toBeNull();
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:8080/games", {
      method: "GET",
      credentials: "include"
    });
  });

  it("throws for unexpected get status", async () => {
    setFetchResponse(500);

    await expect(api.get("/games")).rejects.toThrow("API /games 500");
  });

  it("sends JSON body for post", async () => {
    const payload = { name: "Meeple" };
    setFetchResponse(201, { id: "123" });

    await api.post("/games", payload);

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:8080/games", {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
  });

  it("throws Unauthorized for 401 post", async () => {
    setFetchResponse(401);

    await expect(api.post("/games", {})).rejects.toThrow("Unauthorized");
  });

  it("sends JSON body for put", async () => {
    const payload = { name: "Meeple" };
    setFetchResponse(200, { id: "123" });

    await api.put("/games", payload);

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:8080/games", {
      method: "PUT",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
  });

  it("throws Unauthorized for 401 put", async () => {
    setFetchResponse(401);

    await expect(api.put("/games", {})).rejects.toThrow("Unauthorized");
  });
});
