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

  describe("getApiBase", () => {
    const originalEnv = process.env.NEXT_PUBLIC_API_BASE;

    afterEach(() => {
      process.env.NEXT_PUBLIC_API_BASE = originalEnv;
    });

    it('returns fallback when env is empty string', () => {
      process.env.NEXT_PUBLIC_API_BASE = '';
      const { getApiBase } = require('../api');
      expect(getApiBase()).toBe('http://localhost:8080');
    });

    it('returns fallback when env is literal "undefined" string', () => {
      process.env.NEXT_PUBLIC_API_BASE = 'undefined';
      const { getApiBase } = require('../api');
      expect(getApiBase()).toBe('http://localhost:8080');
    });

    it('returns fallback when env is literal "null" string', () => {
      process.env.NEXT_PUBLIC_API_BASE = 'null';
      const { getApiBase } = require('../api');
      expect(getApiBase()).toBe('http://localhost:8080');
    });

    it('returns fallback when env is whitespace only', () => {
      process.env.NEXT_PUBLIC_API_BASE = '   ';
      const { getApiBase } = require('../api');
      expect(getApiBase()).toBe('http://localhost:8080');
    });

    it('returns env value when valid URL is provided', () => {
      process.env.NEXT_PUBLIC_API_BASE = 'https://api.example.com';
      const { getApiBase } = require('../api');
      expect(getApiBase()).toBe('https://api.example.com');
    });
  });

  it("sends empty object when post body is undefined", async () => {
    setFetchResponse(201, { id: "123" });

    await api.post("/games");

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:8080/games", {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({})
    });
  });

  it("throws for non-ok status in post", async () => {
    setFetchResponse(500);

    await expect(api.post("/games", {})).rejects.toThrow("API /games 500");
  });

  it("throws for non-ok status in put", async () => {
    setFetchResponse(404);

    await expect(api.put("/games", {})).rejects.toThrow("API /games 404");
  });

  describe("delete", () => {
    it("calls DELETE endpoint and returns void for successful deletion", async () => {
      setFetchResponse(204);

      await api.delete("/games/123");

      expect(fetchMock).toHaveBeenCalledWith("http://localhost:8080/games/123", {
        method: "DELETE",
        credentials: "include"
      });
    });

    it("throws Unauthorized for 401 delete", async () => {
      setFetchResponse(401);

      await expect(api.delete("/games/123")).rejects.toThrow("Unauthorized");
    });

    it("throws for non-ok status in delete", async () => {
      setFetchResponse(404);

      await expect(api.delete("/games/123")).rejects.toThrow("API /games/123 404");
    });

    it("handles successful 200 status", async () => {
      setFetchResponse(200);

      await expect(api.delete("/games/123")).resolves.toBeUndefined();
    });
  });

  describe("auth API", () => {
    describe("getSessionStatus", () => {
      it("returns session status for authenticated user", async () => {
        const sessionData = {
          expiresAt: "2025-10-18T12:00:00Z",
          lastSeenAt: "2025-10-17T11:00:00Z",
          remainingMinutes: 60
        };
        setFetchResponse(200, sessionData);

        const result = await api.auth.getSessionStatus();

        expect(result).toEqual(sessionData);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/auth/session/status",
          {
            method: "GET",
            credentials: "include"
          }
        );
      });

      it("returns null for unauthenticated user", async () => {
        setFetchResponse(401);

        const result = await api.auth.getSessionStatus();

        expect(result).toBeNull();
      });

      it("throws for server error", async () => {
        setFetchResponse(500);

        await expect(api.auth.getSessionStatus()).rejects.toThrow(
          "API /api/v1/auth/session/status 500"
        );
      });
    });

    describe("extendSession", () => {
      it("extends session and returns updated status", async () => {
        const sessionData = {
          expiresAt: "2025-10-18T13:00:00Z",
          lastSeenAt: "2025-10-17T12:00:00Z",
          remainingMinutes: 120
        };
        setFetchResponse(200, sessionData);

        const result = await api.auth.extendSession();

        expect(result).toEqual(sessionData);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/auth/session/extend",
          {
            method: "POST",
            credentials: "include",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({})
          }
        );
      });

      it("throws Unauthorized for unauthenticated user", async () => {
        setFetchResponse(401);

        await expect(api.auth.extendSession()).rejects.toThrow("Unauthorized");
      });

      it("throws for server error", async () => {
        setFetchResponse(500);

        await expect(api.auth.extendSession()).rejects.toThrow(
          "API /api/v1/auth/session/extend 500"
        );
      });
    });
  });

  describe("ruleSpecComments API", () => {
    const gameId = "game-123";
    const version = "v1.0.0";
    const commentId = "comment-456";

    describe("getComments", () => {
      it("returns comments for a game version", async () => {
        const commentsData = {
          gameId,
          version,
          comments: [
            {
              id: "comment-1",
              gameId,
              version,
              atomId: "atom-1",
              userId: "user-1",
              userDisplayName: "John Doe",
              commentText: "Great rule!",
              createdAt: "2025-10-17T10:00:00Z",
              updatedAt: null
            }
          ],
          totalComments: 1
        };
        setFetchResponse(200, commentsData);

        const result = await api.ruleSpecComments.getComments(gameId, version);

        expect(result).toEqual(commentsData);
        expect(fetchMock).toHaveBeenCalledWith(
          `http://localhost:8080/api/v1/games/${gameId}/rulespec/versions/${version}/comments`,
          {
            method: "GET",
            credentials: "include"
          }
        );
      });

      it("returns null for unauthenticated user", async () => {
        setFetchResponse(401);

        const result = await api.ruleSpecComments.getComments(gameId, version);

        expect(result).toBeNull();
      });

      it("throws for server error", async () => {
        setFetchResponse(500);

        await expect(
          api.ruleSpecComments.getComments(gameId, version)
        ).rejects.toThrow(
          `API /api/v1/games/${gameId}/rulespec/versions/${version}/comments 500`
        );
      });
    });

    describe("createComment", () => {
      it("creates a new comment", async () => {
        const request = {
          atomId: "atom-1",
          commentText: "This is a great rule!"
        };
        const createdComment = {
          id: "comment-789",
          gameId,
          version,
          atomId: "atom-1",
          userId: "user-1",
          userDisplayName: "John Doe",
          commentText: "This is a great rule!",
          createdAt: "2025-10-17T10:00:00Z",
          updatedAt: null
        };
        setFetchResponse(201, createdComment);

        const result = await api.ruleSpecComments.createComment(
          gameId,
          version,
          request
        );

        expect(result).toEqual(createdComment);
        expect(fetchMock).toHaveBeenCalledWith(
          `http://localhost:8080/api/v1/games/${gameId}/rulespec/versions/${version}/comments`,
          {
            method: "POST",
            credentials: "include",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(request)
          }
        );
      });

      it("creates a comment without atomId", async () => {
        const request = {
          atomId: null,
          commentText: "General comment"
        };
        const createdComment = {
          id: "comment-790",
          gameId,
          version,
          atomId: null,
          userId: "user-1",
          userDisplayName: "John Doe",
          commentText: "General comment",
          createdAt: "2025-10-17T10:00:00Z",
          updatedAt: null
        };
        setFetchResponse(201, createdComment);

        const result = await api.ruleSpecComments.createComment(
          gameId,
          version,
          request
        );

        expect(result).toEqual(createdComment);
      });

      it("throws Unauthorized for unauthenticated user", async () => {
        setFetchResponse(401);

        await expect(
          api.ruleSpecComments.createComment(gameId, version, {
            atomId: null,
            commentText: "Test"
          })
        ).rejects.toThrow("Unauthorized");
      });

      it("throws for validation error", async () => {
        setFetchResponse(400);

        await expect(
          api.ruleSpecComments.createComment(gameId, version, {
            atomId: null,
            commentText: ""
          })
        ).rejects.toThrow(
          `API /api/v1/games/${gameId}/rulespec/versions/${version}/comments 400`
        );
      });
    });

    describe("updateComment", () => {
      it("updates an existing comment", async () => {
        const request = {
          commentText: "Updated comment text"
        };
        const updatedComment = {
          id: commentId,
          gameId,
          version,
          atomId: "atom-1",
          userId: "user-1",
          userDisplayName: "John Doe",
          commentText: "Updated comment text",
          createdAt: "2025-10-17T10:00:00Z",
          updatedAt: "2025-10-17T11:00:00Z"
        };
        setFetchResponse(200, updatedComment);

        const result = await api.ruleSpecComments.updateComment(
          gameId,
          commentId,
          request
        );

        expect(result).toEqual(updatedComment);
        expect(fetchMock).toHaveBeenCalledWith(
          `http://localhost:8080/api/v1/games/${gameId}/rulespec/comments/${commentId}`,
          {
            method: "PUT",
            credentials: "include",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(request)
          }
        );
      });

      it("throws Unauthorized for unauthenticated user", async () => {
        setFetchResponse(401);

        await expect(
          api.ruleSpecComments.updateComment(gameId, commentId, {
            commentText: "Updated"
          })
        ).rejects.toThrow("Unauthorized");
      });

      it("throws for not found error", async () => {
        setFetchResponse(404);

        await expect(
          api.ruleSpecComments.updateComment(gameId, commentId, {
            commentText: "Updated"
          })
        ).rejects.toThrow(
          `API /api/v1/games/${gameId}/rulespec/comments/${commentId} 404`
        );
      });
    });

    describe("deleteComment", () => {
      it("deletes a comment successfully", async () => {
        setFetchResponse(204);

        await api.ruleSpecComments.deleteComment(gameId, commentId);

        expect(fetchMock).toHaveBeenCalledWith(
          `http://localhost:8080/api/v1/games/${gameId}/rulespec/comments/${commentId}`,
          {
            method: "DELETE",
            credentials: "include"
          }
        );
      });

      it("throws Unauthorized for unauthenticated user", async () => {
        setFetchResponse(401);

        await expect(
          api.ruleSpecComments.deleteComment(gameId, commentId)
        ).rejects.toThrow("Unauthorized");
      });

      it("throws for not found error", async () => {
        setFetchResponse(404);

        await expect(
          api.ruleSpecComments.deleteComment(gameId, commentId)
        ).rejects.toThrow(
          `API /api/v1/games/${gameId}/rulespec/comments/${commentId} 404`
        );
      });

      it("throws for forbidden error", async () => {
        setFetchResponse(403);

        await expect(
          api.ruleSpecComments.deleteComment(gameId, commentId)
        ).rejects.toThrow(
          `API /api/v1/games/${gameId}/rulespec/comments/${commentId} 403`
        );
      });
    });
  });
});
