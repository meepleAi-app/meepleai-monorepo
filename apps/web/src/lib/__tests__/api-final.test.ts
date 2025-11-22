import { api } from "../api";
import type {
  BggSearchResponse,
  BggGameDetails,
  SystemConfigurationDto,
  PagedResult,
  CreateConfigurationRequest,
  UpdateConfigurationRequest,
  BulkConfigurationUpdateRequest,
  ConfigurationValidationResult,
  ConfigurationExportDto,
  ConfigurationImportRequest,
  ConfigurationHistoryDto,
  TwoFactorStatusDto,
  TotpSetupResponse
} from "../api";

/**
 * TEST-625: Final coverage for api.ts
 * Covers remaining sections:
 * - AI-13: BoardGameGeek API (Lines 527-547)
 * - CONFIG-06: Dynamic Configuration System (Lines 550-727)
 * - EDIT-07: Bulk RuleSpec Operations (Lines 730-783)
 * - AUTH-07: Two-Factor Authentication (Lines 785-806)
 */
describe("api - Final Coverage", () => {
  let originalFetch: typeof global.fetch;
  let fetchMock: jest.MockedFunction<typeof fetch>;

  // Helper to create fetch response with headers
  const setFetchResponse = (status: number, payload?: unknown, headers?: Record<string, string>) => {
    const headersMap = new Headers(headers);
    fetchMock.mockResolvedValue({
      ok: status >= 200 && status < 300,
      status,
      json: async () => payload,
      blob: async () => new Blob([JSON.stringify(payload || {})]),
      headers: headersMap
    } as Response);
  };

  // Helper to create error response with correlation ID
  const setErrorResponse = (status: number, error?: unknown, correlationId?: string) => {
    const headers = new Headers();
    if (correlationId) {
      headers.set("X-Correlation-Id", correlationId);
    }
    fetchMock.mockResolvedValue({
      ok: false,
      status,
      json: async () => error || {},
      headers
    } as Response);
  };

  beforeEach(() => {
    originalFetch = global.fetch;
    fetchMock = jest.fn() as jest.MockedFunction<typeof fetch>;
    global.fetch = fetchMock;

    // Mock DOM APIs for blob download tests
    document.createElement = jest.fn((tag: string) => {
      if (tag === 'a') {
        return {
          href: '',
          download: '',
          click: jest.fn(),
        } as unknown as HTMLAnchorElement;
      }
      return document.createElement(tag);
    });
    document.body.appendChild = jest.fn();
    document.body.removeChild = jest.fn();
    global.URL.createObjectURL = jest.fn(() => 'blob:test-url');
    global.URL.revokeObjectURL = jest.fn();
  });

  afterEach(() => {
    global.fetch = originalFetch;
    fetchMock.mockReset();
    jest.clearAllMocks();
  });

  // ============================================================================
  // AI-13: BoardGameGeek API (Lines 527-547)
  // ============================================================================
  describe("bgg", () => {
    describe("search", () => {
      it("should search games with query parameter", async () => {
        // Arrange
        const mockResponse: BggSearchResponse = {
          results: [
            {
              bggId: 174430,
              name: "Gloomhaven",
              yearPublished: 2017,
              thumbnailUrl: "https://example.com/thumb.jpg",
              type: "boardgame"
            }
          ]
        };
        setFetchResponse(200, mockResponse);

        // Act
        const result = await api.bgg.search("Gloomhaven");

        // Assert
        expect(result).toEqual(mockResponse);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/bgg/search?q=Gloomhaven",
          expect.objectContaining({ method: "GET" })
        );
      });

      it("should search games with exact match parameter", async () => {
        // Arrange
        const mockResponse: BggSearchResponse = { results: [] };
        setFetchResponse(200, mockResponse);

        // Act
        const result = await api.bgg.search("Catan", true);

        // Assert
        expect(result).toEqual(mockResponse);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/bgg/search?q=Catan&exact=true",
          expect.objectContaining({ method: "GET" })
        );
      });

      it("should URL encode search query", async () => {
        // Arrange
        setFetchResponse(200, { results: [] });

        // Act
        await api.bgg.search("Dungeons & Dragons");

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining("q=Dungeons+%26+Dragons"),
          expect.anything()
        );
      });

      it("should throw error when search returns null", async () => {
        // Arrange
        setFetchResponse(401);

        // Act & Assert
        await expect(api.bgg.search("test")).rejects.toThrow(
          "Failed to search BoardGameGeek"
        );
      });

      it("should handle empty results", async () => {
        // Arrange
        const mockResponse: BggSearchResponse = { results: [] };
        setFetchResponse(200, mockResponse);

        // Act
        const result = await api.bgg.search("nonexistent game");

        // Assert
        expect(result.results).toHaveLength(0);
      });

      it("should handle 500 error from API", async () => {
        // Arrange
        setErrorResponse(500, { error: "BGG API unavailable" }, "corr-123");

        // Act & Assert
        await expect(api.bgg.search("test")).rejects.toThrow("BGG API unavailable");
      });
    });

    describe("getGameDetails", () => {
      it("should fetch game details by BGG ID", async () => {
        // Arrange
        const mockDetails: BggGameDetails = {
          bggId: 174430,
          name: "Gloomhaven",
          description: "Epic campaign game",
          yearPublished: 2017,
          minPlayers: 1,
          maxPlayers: 4,
          playingTime: 120,
          minPlayTime: 60,
          maxPlayTime: 120,
          minAge: 14,
          averageRating: 8.8,
          bayesAverageRating: 8.6,
          usersRated: 50000,
          averageWeight: 3.86,
          thumbnailUrl: "https://example.com/thumb.jpg",
          imageUrl: "https://example.com/image.jpg",
          categories: ["Adventure", "Fantasy"],
          mechanics: ["Cooperative", "Hand Management"],
          designers: ["Isaac Childres"],
          publishers: ["Cephalofair Games"]
        };
        setFetchResponse(200, mockDetails);

        // Act
        const result = await api.bgg.getGameDetails(174430);

        // Assert
        expect(result).toEqual(mockDetails);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/bgg/games/174430",
          expect.objectContaining({ method: "GET" })
        );
      });

      it("should throw error when game not found", async () => {
        // Arrange
        setFetchResponse(401);

        // Act & Assert
        await expect(api.bgg.getGameDetails(999999)).rejects.toThrow(
          "Game with BGG ID 999999 not found"
        );
      });

      it("should handle 404 error", async () => {
        // Arrange
        setErrorResponse(404, { error: "Game not found" }, "corr-456");

        // Act & Assert
        await expect(api.bgg.getGameDetails(123)).rejects.toThrow("Game not found");
      });

      it("should handle null values in game details", async () => {
        // Arrange
        const mockDetails: BggGameDetails = {
          bggId: 123,
          name: "Test Game",
          description: null,
          yearPublished: null,
          minPlayers: null,
          maxPlayers: null,
          playingTime: null,
          minPlayTime: null,
          maxPlayTime: null,
          minAge: null,
          averageRating: null,
          bayesAverageRating: null,
          usersRated: null,
          averageWeight: null,
          thumbnailUrl: null,
          imageUrl: null,
          categories: [],
          mechanics: [],
          designers: [],
          publishers: []
        };
        setFetchResponse(200, mockDetails);

        // Act
        const result = await api.bgg.getGameDetails(123);

        // Assert
        expect(result.description).toBeNull();
        expect(result.yearPublished).toBeNull();
      });
    });
  });

  // ============================================================================
  // CONFIG-06: Dynamic Configuration System (Lines 550-727)
  // ============================================================================
  describe("config", () => {
    describe("getConfigurations", () => {
      it("should fetch all configurations with default parameters", async () => {
        // Arrange
        const mockResponse: PagedResult<SystemConfigurationDto> = {
          items: [
            {
              id: "cfg-1",
              key: "Features:EnableChat",
              value: "true",
              valueType: "bool",
              description: "Enable chat feature",
              category: "Features",
              isActive: true,
              requiresRestart: false,
              environment: "All",
              version: 1,
              previousValue: null,
              createdAt: "2024-01-01T00:00:00Z",
              updatedAt: "2024-01-01T00:00:00Z",
              createdByUserId: "user-1",
              updatedByUserId: null,
              lastToggledAt: null
            }
          ],
          total: 1,
          page: 1,
          pageSize: 50
        };
        setFetchResponse(200, mockResponse);

        // Act
        const result = await api.config.getConfigurations();

        // Assert
        expect(result).toEqual(mockResponse);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations?activeOnly=true&page=1&pageSize=50",
          expect.objectContaining({ method: "GET" })
        );
      });

      it("should apply category filter", async () => {
        // Arrange
        setFetchResponse(200, { items: [], total: 0, page: 1, pageSize: 50 });

        // Act
        await api.config.getConfigurations("AI");

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining("category=AI"),
          expect.anything()
        );
      });

      it("should apply environment filter", async () => {
        // Arrange
        setFetchResponse(200, { items: [], total: 0, page: 1, pageSize: 50 });

        // Act
        await api.config.getConfigurations(undefined, "Production");

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining("environment=Production"),
          expect.anything()
        );
      });

      it("should apply activeOnly=false filter", async () => {
        // Arrange
        setFetchResponse(200, { items: [], total: 0, page: 1, pageSize: 50 });

        // Act
        await api.config.getConfigurations(undefined, undefined, false);

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining("activeOnly=false"),
          expect.anything()
        );
      });

      it("should apply pagination parameters", async () => {
        // Arrange
        setFetchResponse(200, { items: [], total: 0, page: 2, pageSize: 100 });

        // Act
        await api.config.getConfigurations(undefined, undefined, true, 2, 100);

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining("page=2&pageSize=100"),
          expect.anything()
        );
      });

      it("should apply all filters simultaneously", async () => {
        // Arrange
        setFetchResponse(200, { items: [], total: 0, page: 3, pageSize: 25 });

        // Act
        await api.config.getConfigurations("AI", "Development", false, 3, 25);

        // Assert
        const call = fetchMock.mock.calls[0][0] as string;
        expect(call).toContain("category=AI");
        expect(call).toContain("environment=Development");
        expect(call).toContain("activeOnly=false");
        expect(call).toContain("page=3");
        expect(call).toContain("pageSize=25");
      });

      it("should throw error when response is null", async () => {
        // Arrange
        setFetchResponse(401);

        // Act & Assert
        await expect(api.config.getConfigurations()).rejects.toThrow(
          "Failed to fetch configurations"
        );
      });

      it("should handle 500 error", async () => {
        // Arrange
        setErrorResponse(500, { error: "Database error" });

        // Act & Assert
        await expect(api.config.getConfigurations()).rejects.toThrow("Database error");
      });
    });

    describe("getConfiguration", () => {
      it("should fetch configuration by ID", async () => {
        // Arrange
        const mockConfig: SystemConfigurationDto = {
          id: "cfg-1",
          key: "AI:Temperature",
          value: "0.7",
          valueType: "double",
          description: "LLM temperature",
          category: "AI",
          isActive: true,
          requiresRestart: true,
          environment: "All",
          version: 1,
          previousValue: null,
          createdAt: "2024-01-01T00:00:00Z",
          updatedAt: "2024-01-01T00:00:00Z",
          createdByUserId: "user-1",
          updatedByUserId: null,
          lastToggledAt: null
        };
        setFetchResponse(200, mockConfig);

        // Act
        const result = await api.config.getConfiguration("cfg-1");

        // Assert
        expect(result).toEqual(mockConfig);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/cfg-1",
          expect.objectContaining({ method: "GET" })
        );
      });

      it("should throw error when configuration not found", async () => {
        // Arrange
        setFetchResponse(401);

        // Act & Assert
        await expect(api.config.getConfiguration("nonexistent")).rejects.toThrow(
          "Configuration nonexistent not found"
        );
      });

      it("should handle 404 error", async () => {
        // Arrange
        setErrorResponse(404, { error: "Not found" });

        // Act & Assert
        await expect(api.config.getConfiguration("cfg-999")).rejects.toThrow("Not found");
      });
    });

    describe("getConfigurationByKey", () => {
      it("should fetch configuration by key without environment", async () => {
        // Arrange
        const mockConfig: SystemConfigurationDto = {
          id: "cfg-1",
          key: "Features:EnableChat",
          value: "true",
          valueType: "bool",
          description: null,
          category: "Features",
          isActive: true,
          requiresRestart: false,
          environment: "All",
          version: 1,
          previousValue: null,
          createdAt: "2024-01-01T00:00:00Z",
          updatedAt: "2024-01-01T00:00:00Z",
          createdByUserId: "user-1",
          updatedByUserId: null,
          lastToggledAt: null
        };
        setFetchResponse(200, mockConfig);

        // Act
        const result = await api.config.getConfigurationByKey("Features:EnableChat");

        // Assert
        expect(result).toEqual(mockConfig);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/key/Features%3AEnableChat?",
          expect.objectContaining({ method: "GET" })
        );
      });

      it("should URL encode key with special characters", async () => {
        // Arrange
        setFetchResponse(200, {} as SystemConfigurationDto);

        // Act
        await api.config.getConfigurationByKey("AI:Model/Version");

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining("AI%3AModel%2FVersion"),
          expect.anything()
        );
      });

      it("should include environment parameter", async () => {
        // Arrange
        setFetchResponse(200, {} as SystemConfigurationDto);

        // Act
        await api.config.getConfigurationByKey("AI:Temperature", "Production");

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining("environment=Production"),
          expect.anything()
        );
      });

      it("should throw error when key not found", async () => {
        // Arrange
        setFetchResponse(401);

        // Act & Assert
        await expect(
          api.config.getConfigurationByKey("Invalid:Key")
        ).rejects.toThrow("Configuration with key 'Invalid:Key' not found");
      });
    });

    describe("createConfiguration", () => {
      it("should create new configuration", async () => {
        // Arrange
        const request: CreateConfigurationRequest = {
          key: "Features:NewFeature",
          value: "true",
          valueType: "bool",
          description: "New feature toggle",
          category: "Features",
          isActive: true,
          requiresRestart: false,
          environment: "Development"
        };
        const mockResponse: SystemConfigurationDto = {
          id: "cfg-new",
          ...request,
          value: "true",
          valueType: "bool",
          description: "New feature toggle",
          category: "Features",
          isActive: true,
          requiresRestart: false,
          environment: "Development",
          version: 1,
          previousValue: null,
          createdAt: "2024-01-01T00:00:00Z",
          updatedAt: "2024-01-01T00:00:00Z",
          createdByUserId: "user-1",
          updatedByUserId: null,
          lastToggledAt: null
        };
        setFetchResponse(201, mockResponse);

        // Act
        const result = await api.config.createConfiguration(request);

        // Assert
        expect(result).toEqual(mockResponse);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations",
          expect.objectContaining({
            method: "POST",
            body: JSON.stringify(request)
          })
        );
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        setErrorResponse(401, {}, "corr-auth");

        // Act & Assert
        await expect(
          api.config.createConfiguration({ key: "test", value: "val" })
        ).rejects.toThrow("Unauthorized");
      });

      it("should handle validation error", async () => {
        // Arrange
        setErrorResponse(400, { error: "Invalid key format" });

        // Act & Assert
        await expect(
          api.config.createConfiguration({ key: "invalid key", value: "val" })
        ).rejects.toThrow("Invalid key format");
      });
    });

    describe("updateConfiguration", () => {
      it("should update existing configuration", async () => {
        // Arrange
        const request: UpdateConfigurationRequest = {
          value: "0.8",
          description: "Updated description"
        };
        const mockResponse: SystemConfigurationDto = {
          id: "cfg-1",
          key: "AI:Temperature",
          value: "0.8",
          valueType: "double",
          description: "Updated description",
          category: "AI",
          isActive: true,
          requiresRestart: true,
          environment: "All",
          version: 2,
          previousValue: "0.7",
          createdAt: "2024-01-01T00:00:00Z",
          updatedAt: "2024-01-02T00:00:00Z",
          createdByUserId: "user-1",
          updatedByUserId: "user-1",
          lastToggledAt: null
        };
        setFetchResponse(200, mockResponse);

        // Act
        const result = await api.config.updateConfiguration("cfg-1", request);

        // Assert
        expect(result).toEqual(mockResponse);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/cfg-1",
          expect.objectContaining({
            method: "PUT",
            body: JSON.stringify(request)
          })
        );
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        setErrorResponse(401);

        // Act & Assert
        await expect(
          api.config.updateConfiguration("cfg-1", { value: "new" })
        ).rejects.toThrow("Unauthorized");
      });

      it("should handle 404 Not Found", async () => {
        // Arrange
        setErrorResponse(404, { error: "Configuration not found" });

        // Act & Assert
        await expect(
          api.config.updateConfiguration("cfg-999", { value: "new" })
        ).rejects.toThrow("Configuration not found");
      });
    });

    describe("deleteConfiguration", () => {
      it("should delete configuration by ID", async () => {
        // Arrange
        setFetchResponse(204);

        // Act
        await api.config.deleteConfiguration("cfg-1");

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/cfg-1",
          expect.objectContaining({ method: "DELETE" })
        );
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        setErrorResponse(401);

        // Act & Assert
        await expect(api.config.deleteConfiguration("cfg-1")).rejects.toThrow(
          "Unauthorized"
        );
      });

      it("should handle 403 Forbidden", async () => {
        // Arrange
        setErrorResponse(403, { error: "Insufficient permissions" });

        // Act & Assert
        await expect(api.config.deleteConfiguration("cfg-1")).rejects.toThrow(
          "Insufficient permissions"
        );
      });
    });

    describe("bulkUpdate", () => {
      it("should update multiple configurations", async () => {
        // Arrange
        const request: BulkConfigurationUpdateRequest = {
          updates: [
            { id: "cfg-1", value: "0.8" },
            { id: "cfg-2", value: "1000" }
          ]
        };
        const mockResponse: SystemConfigurationDto[] = [
          {
            id: "cfg-1",
            key: "AI:Temperature",
            value: "0.8",
            valueType: "double",
            description: null,
            category: "AI",
            isActive: true,
            requiresRestart: true,
            environment: "All",
            version: 2,
            previousValue: "0.7",
            createdAt: "2024-01-01T00:00:00Z",
            updatedAt: "2024-01-02T00:00:00Z",
            createdByUserId: "user-1",
            updatedByUserId: "user-1",
            lastToggledAt: null
          }
        ];
        setFetchResponse(200, mockResponse);

        // Act
        const result = await api.config.bulkUpdate(request);

        // Assert
        expect(result).toEqual(mockResponse);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/bulk-update",
          expect.objectContaining({
            method: "POST",
            body: JSON.stringify(request)
          })
        );
      });

      it("should handle empty updates array", async () => {
        // Arrange
        setFetchResponse(200, []);

        // Act
        const result = await api.config.bulkUpdate({ updates: [] });

        // Assert
        expect(result).toEqual([]);
      });

      it("should handle partial success errors", async () => {
        // Arrange
        setErrorResponse(400, { error: "Some updates failed" });

        // Act & Assert
        await expect(
          api.config.bulkUpdate({ updates: [{ id: "invalid", value: "bad" }] })
        ).rejects.toThrow("Some updates failed");
      });
    });

    describe("validateConfiguration", () => {
      it("should validate valid configuration", async () => {
        // Arrange
        const mockResponse: ConfigurationValidationResult = {
          isValid: true,
          errors: []
        };
        setFetchResponse(200, mockResponse);

        // Act
        const result = await api.config.validateConfiguration(
          "AI:Temperature",
          "0.7",
          "double"
        );

        // Assert
        expect(result).toEqual(mockResponse);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/validate",
          expect.objectContaining({
            method: "POST",
            body: JSON.stringify({
              key: "AI:Temperature",
              value: "0.7",
              valueType: "double"
            })
          })
        );
      });

      it("should return validation errors", async () => {
        // Arrange
        const mockResponse: ConfigurationValidationResult = {
          isValid: false,
          errors: ["Value must be between 0 and 1"]
        };
        setFetchResponse(200, mockResponse);

        // Act
        const result = await api.config.validateConfiguration(
          "AI:Temperature",
          "2.5",
          "double"
        );

        // Assert
        expect(result.isValid).toBe(false);
        expect(result.errors).toHaveLength(1);
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        setErrorResponse(401);

        // Act & Assert
        await expect(
          api.config.validateConfiguration("key", "value", "string")
        ).rejects.toThrow("Unauthorized");
      });
    });

    describe("exportConfigurations", () => {
      it("should export configurations for environment", async () => {
        // Arrange
        const mockResponse: ConfigurationExportDto = {
          configurations: [
            {
              id: "cfg-1",
              key: "AI:Temperature",
              value: "0.7",
              valueType: "double",
              description: null,
              category: "AI",
              isActive: true,
              requiresRestart: true,
              environment: "Production",
              version: 1,
              previousValue: null,
              createdAt: "2024-01-01T00:00:00Z",
              updatedAt: "2024-01-01T00:00:00Z",
              createdByUserId: "user-1",
              updatedByUserId: null,
              lastToggledAt: null
            }
          ],
          exportedAt: "2024-01-01T00:00:00Z",
          environment: "Production"
        };
        setFetchResponse(200, mockResponse);

        // Act
        const result = await api.config.exportConfigurations("Production");

        // Assert
        expect(result).toEqual(mockResponse);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/export?environment=Production&activeOnly=true",
          expect.objectContaining({ method: "GET" })
        );
      });

      it("should include inactive configurations", async () => {
        // Arrange
        setFetchResponse(200, {
          configurations: [],
          exportedAt: "2024-01-01T00:00:00Z",
          environment: "Development"
        });

        // Act
        await api.config.exportConfigurations("Development", false);

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining("activeOnly=false"),
          expect.anything()
        );
      });

      it("should throw error when export fails", async () => {
        // Arrange
        setFetchResponse(401);

        // Act & Assert
        await expect(
          api.config.exportConfigurations("Production")
        ).rejects.toThrow("Failed to export configurations");
      });
    });

    describe("importConfigurations", () => {
      it("should import configurations", async () => {
        // Arrange
        const request: ConfigurationImportRequest = {
          configurations: [
            {
              key: "AI:Temperature",
              value: "0.7",
              valueType: "double"
            }
          ],
          overwriteExisting: true
        };
        setFetchResponse(200, 1);

        // Act
        const result = await api.config.importConfigurations(request);

        // Assert
        expect(result).toBe(1);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/import",
          expect.objectContaining({
            method: "POST",
            body: JSON.stringify(request)
          })
        );
      });

      it("should handle import without overwrite", async () => {
        // Arrange
        const request: ConfigurationImportRequest = {
          configurations: [],
          overwriteExisting: false
        };
        setFetchResponse(200, 0);

        // Act
        await api.config.importConfigurations(request);

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.anything(),
          expect.objectContaining({
            body: expect.stringContaining('"overwriteExisting":false')
          })
        );
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        setErrorResponse(401);

        // Act & Assert
        await expect(
          api.config.importConfigurations({ configurations: [] })
        ).rejects.toThrow("Unauthorized");
      });
    });

    describe("getHistory", () => {
      it("should fetch configuration history with default limit", async () => {
        // Arrange
        const mockHistory: ConfigurationHistoryDto[] = [
          {
            id: "hist-1",
            configurationId: "cfg-1",
            key: "AI:Temperature",
            oldValue: "0.7",
            newValue: "0.8",
            version: 2,
            changedAt: "2024-01-02T00:00:00Z",
            changedByUserId: "user-1",
            changeReason: "Performance tuning"
          }
        ];
        setFetchResponse(200, mockHistory);

        // Act
        const result = await api.config.getHistory("cfg-1");

        // Assert
        expect(result).toEqual(mockHistory);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/cfg-1/history?limit=20",
          expect.objectContaining({ method: "GET" })
        );
      });

      it("should apply custom limit", async () => {
        // Arrange
        setFetchResponse(200, []);

        // Act
        await api.config.getHistory("cfg-1", 50);

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining("limit=50"),
          expect.anything()
        );
      });

      it("should return empty array when history is null", async () => {
        // Arrange
        setFetchResponse(401);

        // Act
        const result = await api.config.getHistory("cfg-1");

        // Assert
        expect(result).toEqual([]);
      });

      it("should handle 404 for nonexistent configuration", async () => {
        // Arrange
        setErrorResponse(404, { error: "Configuration not found" });

        // Act & Assert
        await expect(api.config.getHistory("cfg-999")).rejects.toThrow(
          "Configuration not found"
        );
      });
    });

    describe("rollback", () => {
      it("should rollback configuration to previous version", async () => {
        // Arrange
        const mockResponse: SystemConfigurationDto = {
          id: "cfg-1",
          key: "AI:Temperature",
          value: "0.7",
          valueType: "double",
          description: null,
          category: "AI",
          isActive: true,
          requiresRestart: true,
          environment: "All",
          version: 3,
          previousValue: "0.8",
          createdAt: "2024-01-01T00:00:00Z",
          updatedAt: "2024-01-03T00:00:00Z",
          createdByUserId: "user-1",
          updatedByUserId: "user-1",
          lastToggledAt: null
        };
        setFetchResponse(200, mockResponse);

        // Act
        const result = await api.config.rollback("cfg-1", 1);

        // Assert
        expect(result).toEqual(mockResponse);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/cfg-1/rollback/1",
          expect.objectContaining({
            method: "POST",
            body: JSON.stringify({})
          })
        );
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        setErrorResponse(401);

        // Act & Assert
        await expect(api.config.rollback("cfg-1", 1)).rejects.toThrow(
          "Unauthorized"
        );
      });

      it("should handle invalid version", async () => {
        // Arrange
        setErrorResponse(400, { error: "Invalid version" });

        // Act & Assert
        await expect(api.config.rollback("cfg-1", 999)).rejects.toThrow(
          "Invalid version"
        );
      });
    });

    describe("getCategories", () => {
      it("should fetch all unique categories", async () => {
        // Arrange
        const mockCategories = ["AI", "Features", "RateLimit", "RAG"];
        setFetchResponse(200, mockCategories);

        // Act
        const result = await api.config.getCategories();

        // Assert
        expect(result).toEqual(mockCategories);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/categories",
          expect.objectContaining({ method: "GET" })
        );
      });

      it("should return empty array when response is null", async () => {
        // Arrange
        setFetchResponse(401);

        // Act
        const result = await api.config.getCategories();

        // Assert
        expect(result).toEqual([]);
      });

      it("should handle empty categories", async () => {
        // Arrange
        setFetchResponse(200, []);

        // Act
        const result = await api.config.getCategories();

        // Assert
        expect(result).toEqual([]);
      });
    });

    describe("invalidateCache", () => {
      it("should invalidate entire configuration cache", async () => {
        // Arrange
        setFetchResponse(204);

        // Act
        await api.config.invalidateCache();

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/cache/invalidate",
          expect.objectContaining({
            method: "POST",
            body: JSON.stringify({})
          })
        );
      });

      it("should invalidate cache for specific key", async () => {
        // Arrange
        setFetchResponse(204);

        // Act
        await api.config.invalidateCache("AI:Temperature");

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/admin/configurations/cache/invalidate",
          expect.objectContaining({
            method: "POST",
            body: JSON.stringify({ key: "AI:Temperature" })
          })
        );
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        setErrorResponse(401);

        // Act & Assert
        await expect(api.config.invalidateCache()).rejects.toThrow(
          "Unauthorized"
        );
      });
    });
  });

  // ============================================================================
  // EDIT-07: Bulk RuleSpec Operations (Lines 730-783)
  // ============================================================================
  describe("ruleSpecs", () => {
    describe("bulkExport", () => {
      it("should export rule specs as ZIP with default filename", async () => {
        // Arrange
        const ruleSpecIds = ["game-1", "game-2"];
        const mockBlob = new Blob(["mock zip content"], { type: "application/zip" });
        fetchMock.mockResolvedValue({
          ok: true,
          status: 200,
          blob: async () => mockBlob,
          headers: new Headers()
        } as Response);

        // Act
        await api.ruleSpecs.bulkExport(ruleSpecIds);

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/rulespecs/bulk/export",
          expect.objectContaining({
            method: "POST",
            body: JSON.stringify({ ruleSpecIds })
          })
        );
        expect(document.createElement).toHaveBeenCalledWith("a");
        expect(document.body.appendChild).toHaveBeenCalled();
        expect(document.body.removeChild).toHaveBeenCalled();
        expect(URL.createObjectURL).toHaveBeenCalledWith(mockBlob);
        expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:test-url");
      });

      it("should extract filename from Content-Disposition header", async () => {
        // Arrange
        const headers = new Headers({
          "Content-Disposition": 'attachment; filename="custom-export.zip"'
        });
        fetchMock.mockResolvedValue({
          ok: true,
          status: 200,
          blob: async () => new Blob([]),
          headers
        } as Response);

        // Mock createElement to return a link we can inspect
        const mockLink = {
          href: '',
          download: '',
          click: jest.fn(),
        };
        (document.createElement as jest.Mock).mockReturnValue(mockLink);

        // Act
        await api.ruleSpecs.bulkExport(["game-1"]);

        // Assert
        expect(mockLink.download).toBe("custom-export.zip");
      });

      it("should handle filename with single quotes in Content-Disposition", async () => {
        // Arrange
        const headers = new Headers({
          "Content-Disposition": "attachment; filename='quoted-file.zip'"
        });
        fetchMock.mockResolvedValue({
          ok: true,
          status: 200,
          blob: async () => new Blob([]),
          headers
        } as Response);

        const mockLink = {
          href: '',
          download: '',
          click: jest.fn(),
        };
        (document.createElement as jest.Mock).mockReturnValue(mockLink);

        // Act
        await api.ruleSpecs.bulkExport(["game-1"]);

        // Assert
        expect(mockLink.download).toBe("quoted-file.zip");
      });

      it("should use default filename when Content-Disposition is missing", async () => {
        // Arrange
        fetchMock.mockResolvedValue({
          ok: true,
          status: 200,
          blob: async () => new Blob([]),
          headers: new Headers()
        } as Response);

        const mockLink = {
          href: '',
          download: '',
          click: jest.fn(),
        };
        (document.createElement as jest.Mock).mockReturnValue(mockLink);

        // Act
        await api.ruleSpecs.bulkExport(["game-1"]);

        // Assert
        expect(mockLink.download).toMatch(/^meepleai-rulespecs-\d{4}-\d{2}-\d{2}\.zip$/);
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        const headers = new Headers({ "X-Correlation-Id": "corr-auth-123" });
        fetchMock.mockResolvedValue({
          ok: false,
          status: 401,
          headers
        } as Response);

        // Act & Assert
        await expect(api.ruleSpecs.bulkExport(["game-1"])).rejects.toMatchObject({
          message: "Unauthorized",
          statusCode: 401,
          correlationId: "corr-auth-123"
        });
      });

      it("should handle 403 Forbidden with specific message", async () => {
        // Arrange
        const headers = new Headers({ "X-Correlation-Id": "corr-forbidden" });
        fetchMock.mockResolvedValue({
          ok: false,
          status: 403,
          headers
        } as Response);

        // Act & Assert
        await expect(api.ruleSpecs.bulkExport(["game-1"])).rejects.toMatchObject({
          message: "Forbidden - Editor or Admin role required",
          statusCode: 403,
          correlationId: "corr-forbidden"
        });
      });

      it("should handle 404 Not Found", async () => {
        // Arrange
        fetchMock.mockResolvedValue({
          ok: false,
          status: 404,
          json: async () => ({ error: "Some rule specs not found" }),
          headers: new Headers()
        } as Response);

        // Act & Assert
        await expect(api.ruleSpecs.bulkExport(["nonexistent"])).rejects.toThrow(
          "Some rule specs not found"
        );
      });

      it("should handle 500 Internal Server Error", async () => {
        // Arrange
        fetchMock.mockResolvedValue({
          ok: false,
          status: 500,
          json: async () => ({ error: "ZIP creation failed" }),
          headers: new Headers()
        } as Response);

        // Act & Assert
        await expect(api.ruleSpecs.bulkExport(["game-1"])).rejects.toThrow(
          "ZIP creation failed"
        );
      });

      it("should handle empty ruleSpecIds array", async () => {
        // Arrange
        fetchMock.mockResolvedValue({
          ok: true,
          status: 200,
          blob: async () => new Blob([]),
          headers: new Headers()
        } as Response);

        // Act
        await api.ruleSpecs.bulkExport([]);

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.anything(),
          expect.objectContaining({
            body: JSON.stringify({ ruleSpecIds: [] })
          })
        );
      });

      it("should handle large number of rule spec IDs", async () => {
        // Arrange
        const largeArray = Array.from({ length: 100 }, (_, i) => `game-${i}`);
        fetchMock.mockResolvedValue({
          ok: true,
          status: 200,
          blob: async () => new Blob([]),
          headers: new Headers()
        } as Response);

        // Act
        await api.ruleSpecs.bulkExport(largeArray);

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.anything(),
          expect.objectContaining({
            body: JSON.stringify({ ruleSpecIds: largeArray })
          })
        );
      });

      it("should clean up DOM elements after download", async () => {
        // Arrange
        fetchMock.mockResolvedValue({
          ok: true,
          status: 200,
          blob: async () => new Blob([]),
          headers: new Headers()
        } as Response);

        const mockLink = {
          href: '',
          download: '',
          click: jest.fn(),
        };
        (document.createElement as jest.Mock).mockReturnValue(mockLink);

        // Act
        await api.ruleSpecs.bulkExport(["game-1"]);

        // Assert
        expect(document.body.removeChild).toHaveBeenCalledWith(mockLink);
        expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:test-url");
      });
    });
  });

  // ============================================================================
  // AUTH-07: Two-Factor Authentication (Lines 785-806)
  // ============================================================================
  describe("twoFactor", () => {
    describe("getStatus", () => {
      it("should fetch 2FA status", async () => {
        // Arrange
        const mockStatus: TwoFactorStatusDto = {
          isEnabled: true,
          enabledAt: '2024-01-01T00:00:00Z',
          unusedBackupCodesCount: 8
        };
        setFetchResponse(200, mockStatus);

        // Act
        const result = await api.twoFactor.getStatus();

        // Assert
        expect(result).toEqual(mockStatus);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/users/me/2fa/status",
          expect.objectContaining({ method: "GET" })
        );
      });

      it("should handle disabled 2FA status", async () => {
        // Arrange
        const mockStatus: TwoFactorStatusDto = {
          isEnabled: false,
          enabledAt: null,
          unusedBackupCodesCount: 0
        };
        setFetchResponse(200, mockStatus);

        // Act
        const result = await api.twoFactor.getStatus();

        // Assert
        expect(result.isEnabled).toBe(false);
        expect(result.unusedBackupCodesCount).toBe(0);
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        setFetchResponse(401);

        // Act
        const result = await api.twoFactor.getStatus();

        // Assert
        expect(result).toBeNull();
      });

      it("should handle 500 error", async () => {
        // Arrange
        setErrorResponse(500, { error: "Database error" });

        // Act & Assert
        await expect(api.twoFactor.getStatus()).rejects.toThrow("Database error");
      });
    });

    describe("setup", () => {
      it("should initiate 2FA setup and return TOTP details", async () => {
        // Arrange
        const mockSetup: TotpSetupResponse = {
          secret: "JBSWY3DPEHPK3PXP",
          qrCodeUri: "otpauth://totp/MeepleAI:user@example.com?secret=JBSWY3DPEHPK3PXP&issuer=MeepleAI",
          backupCodes: [
            "1234-5678",
            "8765-4321",
            "1111-2222",
            "3333-4444",
            "5555-6666",
            "7777-8888",
            "9999-0000",
            "AAAA-BBBB",
            "CCCC-DDDD",
            "EEEE-FFFF"
          ]
        };
        setFetchResponse(200, mockSetup);

        // Act
        const result = await api.twoFactor.setup();

        // Assert
        expect(result).toEqual(mockSetup);
        expect(result.backupCodes).toHaveLength(10);
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/auth/2fa/setup",
          expect.objectContaining({
            method: "POST",
            body: JSON.stringify({})
          })
        );
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        setErrorResponse(401, {}, "corr-2fa");

        // Act & Assert
        await expect(api.twoFactor.setup()).rejects.toMatchObject({
          message: "Unauthorized",
          statusCode: 401,
          correlationId: "corr-2fa"
        });
      });

      it("should handle setup already initiated", async () => {
        // Arrange
        setErrorResponse(400, { error: "2FA already enabled" });

        // Act & Assert
        await expect(api.twoFactor.setup()).rejects.toThrow("2FA already enabled");
      });
    });

    describe("enable", () => {
      it("should enable 2FA with valid code", async () => {
        // Arrange
        setFetchResponse(204);

        // Act
        await api.twoFactor.enable("123456");

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/auth/2fa/enable",
          expect.objectContaining({
            method: "POST",
            body: JSON.stringify({ code: "123456" })
          })
        );
      });

      it("should handle invalid verification code", async () => {
        // Arrange
        setErrorResponse(400, { error: "Invalid verification code" });

        // Act & Assert
        await expect(api.twoFactor.enable("000000")).rejects.toThrow(
          "Invalid verification code"
        );
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        setErrorResponse(401);

        // Act & Assert
        await expect(api.twoFactor.enable("123456")).rejects.toThrow(
          "Unauthorized"
        );
      });

      it("should handle already enabled", async () => {
        // Arrange
        setErrorResponse(409, { error: "2FA already enabled" });

        // Act & Assert
        await expect(api.twoFactor.enable("123456")).rejects.toThrow(
          "2FA already enabled"
        );
      });
    });

    describe("verify", () => {
      it("should verify 2FA code during login", async () => {
        // Arrange
        setFetchResponse(204);

        // Act
        await api.twoFactor.verify("654321");

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/auth/2fa/verify",
          expect.objectContaining({
            method: "POST",
            body: JSON.stringify({ code: "654321" })
          })
        );
      });

      it("should handle backup code verification", async () => {
        // Arrange
        setFetchResponse(204);

        // Act
        await api.twoFactor.verify("1234-5678");

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          expect.anything(),
          expect.objectContaining({
            body: JSON.stringify({ code: "1234-5678" })
          })
        );
      });

      it("should handle invalid code", async () => {
        // Arrange
        setErrorResponse(400, { error: "Invalid code" });

        // Act & Assert
        await expect(api.twoFactor.verify("000000")).rejects.toThrow(
          "Invalid code"
        );
      });

      it("should handle rate limiting", async () => {
        // Arrange
        setErrorResponse(429, { error: "Too many attempts" });

        // Act & Assert
        await expect(api.twoFactor.verify("123456")).rejects.toThrow(
          "Too many attempts"
        );
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        setErrorResponse(401);

        // Act & Assert
        await expect(api.twoFactor.verify("123456")).rejects.toThrow(
          "Unauthorized"
        );
      });
    });

    describe("disable", () => {
      it("should disable 2FA with password and code", async () => {
        // Arrange
        setFetchResponse(204);

        // Act
        await api.twoFactor.disable("MyPassword123!", "123456");

        // Assert
        expect(fetchMock).toHaveBeenCalledWith(
          "http://localhost:8080/api/v1/auth/2fa/disable",
          expect.objectContaining({
            method: "POST",
            body: JSON.stringify({
              password: "MyPassword123!",
              code: "123456"
            })
          })
        );
      });

      it("should handle incorrect password", async () => {
        // Arrange
        setErrorResponse(400, { error: "Invalid password" });

        // Act & Assert
        await expect(
          api.twoFactor.disable("WrongPassword", "123456")
        ).rejects.toThrow("Invalid password");
      });

      it("should handle invalid verification code", async () => {
        // Arrange
        setErrorResponse(400, { error: "Invalid verification code" });

        // Act & Assert
        await expect(
          api.twoFactor.disable("MyPassword123!", "000000")
        ).rejects.toThrow("Invalid verification code");
      });

      it("should handle 2FA not enabled", async () => {
        // Arrange
        setErrorResponse(409, { error: "2FA is not enabled" });

        // Act & Assert
        await expect(
          api.twoFactor.disable("MyPassword123!", "123456")
        ).rejects.toThrow("2FA is not enabled");
      });

      it("should handle 401 Unauthorized", async () => {
        // Arrange
        setErrorResponse(401);

        // Act & Assert
        await expect(
          api.twoFactor.disable("password", "123456")
        ).rejects.toThrow("Unauthorized");
      });
    });
  });
});
