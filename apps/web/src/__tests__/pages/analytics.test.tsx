import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import Analytics from "../admin/analytics";
import { api } from "../../lib/api";

// Mock the api module
jest.mock("../../lib/api");
const mockApi = api as jest.Mocked<typeof api>;

// Mock recharts to avoid canvas rendering issues in tests
jest.mock("recharts", () => ({
  ResponsiveContainer: ({ children }: any) => <div>{children}</div>,
  LineChart: ({ children }: any) => <div data-testid="line-chart">{children}</div>,
  Line: () => <div data-testid="line" />,
  XAxis: () => <div data-testid="x-axis" />,
  YAxis: () => <div data-testid="y-axis" />,
  CartesianGrid: () => <div data-testid="grid" />,
  Tooltip: () => <div data-testid="tooltip" />,
  Legend: () => <div data-testid="legend" />,
}));

// Mock next/link
jest.mock("next/link", () => {
  const MockLink = ({ children, href }: any) => <a href={href}>{children}</a>;
  MockLink.displayName = "Link";
  return MockLink;
});

const mockDashboardStats = {
  metrics: {
    totalUsers: 150,
    activeSessions: 42,
    apiRequestsToday: 1250,
    totalPdfDocuments: 35,
    totalChatMessages: 8420,
    averageConfidenceScore: 0.87,
    totalRagRequests: 5320,
    totalTokensUsed: 1250000,
  },
  userTrend: [
    { date: "2025-10-18T00:00:00Z", count: 5, averageValue: null },
    { date: "2025-10-19T00:00:00Z", count: 8, averageValue: null },
    { date: "2025-10-20T00:00:00Z", count: 12, averageValue: null },
  ],
  sessionTrend: [
    { date: "2025-10-18T00:00:00Z", count: 20, averageValue: null },
    { date: "2025-10-19T00:00:00Z", count: 25, averageValue: null },
    { date: "2025-10-20T00:00:00Z", count: 30, averageValue: null },
  ],
  apiRequestTrend: [
    { date: "2025-10-18T00:00:00Z", count: 400, averageValue: 0.85 },
    { date: "2025-10-19T00:00:00Z", count: 450, averageValue: 0.88 },
    { date: "2025-10-20T00:00:00Z", count: 500, averageValue: 0.86 },
  ],
  pdfUploadTrend: [
    { date: "2025-10-18T00:00:00Z", count: 2, averageValue: 25.5 },
    { date: "2025-10-19T00:00:00Z", count: 3, averageValue: 30.0 },
    { date: "2025-10-20T00:00:00Z", count: 1, averageValue: 20.0 },
  ],
  chatMessageTrend: [
    { date: "2025-10-18T00:00:00Z", count: 100, averageValue: null },
    { date: "2025-10-19T00:00:00Z", count: 150, averageValue: null },
    { date: "2025-10-20T00:00:00Z", count: 200, averageValue: null },
  ],
  generatedAt: "2025-10-25T18:00:00Z",
};

describe("Analytics Dashboard", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  it("renders loading state initially", () => {
    mockApi.get.mockImplementation(() => new Promise(() => {})); // Never resolves

    render(<Analytics />);

    // Loading skeleton should be visible (animate-pulse class)
    const loadingContainer = document.querySelector(".animate-pulse");
    expect(loadingContainer).toBeInTheDocument();
  });

  it("displays dashboard metrics after successful fetch", async () => {
    mockApi.get.mockResolvedValue(mockDashboardStats);

    render(<Analytics />);

    await waitFor(() => {
      expect(screen.getByText("150")).toBeInTheDocument(); // Total users
    }, { timeout: 3000 });

    expect(screen.getByText("42")).toBeInTheDocument(); // Active sessions
    expect(screen.getByText("1,250")).toBeInTheDocument(); // API requests today
    expect(screen.getByText("35")).toBeInTheDocument(); // Total PDFs
    expect(screen.getByText("8,420")).toBeInTheDocument(); // Total chat messages
    expect(screen.getByText("87.0%")).toBeInTheDocument(); // Avg confidence
  });

  it("displays metric cards with correct titles", async () => {
    mockApi.get.mockResolvedValueOnce(mockDashboardStats);

    render(<Analytics />);

    await waitFor(() => {
      expect(screen.getByText("Total Users")).toBeInTheDocument();
      expect(screen.getByText("Active Sessions")).toBeInTheDocument();
      expect(screen.getByText("API Requests Today")).toBeInTheDocument();
      expect(screen.getByText("Total PDF Documents")).toBeInTheDocument();
      expect(screen.getByText("Total Chat Messages")).toBeInTheDocument();
      expect(screen.getByText("Avg Confidence Score")).toBeInTheDocument();
      expect(screen.getByText("Total RAG Requests")).toBeInTheDocument();
      expect(screen.getByText("Total Tokens Used")).toBeInTheDocument();
    });
  });

  it("renders charts with data", async () => {
    mockApi.get.mockResolvedValueOnce(mockDashboardStats);

    render(<Analytics />);

    await waitFor(() => {
      expect(screen.getByText("User Registrations")).toBeInTheDocument();
      expect(screen.getByText("Session Creations")).toBeInTheDocument();
      expect(screen.getByText("API Requests")).toBeInTheDocument();
      expect(screen.getByText("PDF Uploads")).toBeInTheDocument();
      expect(screen.getByText("Chat Messages")).toBeInTheDocument();

      // Check that charts are rendered
      const charts = screen.getAllByTestId("line-chart");
      expect(charts.length).toBe(5);
    });
  });

  it("allows changing time period filter", async () => {
    mockApi.get.mockResolvedValue(mockDashboardStats);

    render(<Analytics />);

    await waitFor(() => {
      expect(screen.getByText("150")).toBeInTheDocument();
    });

    // Clear previous calls
    mockApi.get.mockClear();

    // Change to 7 days
    const select = screen.getByLabelText("Time Period");
    fireEvent.change(select, { target: { value: "7" } });

    await waitFor(() => {
      expect(mockApi.get).toHaveBeenCalledWith(expect.stringContaining("days=7"));
    });
  });

  it("refreshes data when refresh button clicked", async () => {
    mockApi.get.mockResolvedValue(mockDashboardStats);

    render(<Analytics />);

    await waitFor(() => {
      expect(screen.getByText("150")).toBeInTheDocument();
    });

    const refreshButton = screen.getByText("Refresh");
    fireEvent.click(refreshButton);

    await waitFor(() => {
      expect(mockApi.get).toHaveBeenCalledTimes(2);
    });
  });

  it("toggles auto-refresh on and off", async () => {
    mockApi.get.mockResolvedValue(mockDashboardStats);

    render(<Analytics />);

    await waitFor(() => {
      expect(screen.getByText("150")).toBeInTheDocument();
    });

    const autoRefreshButton = screen.getByText("Auto-refresh ON");
    fireEvent.click(autoRefreshButton);

    expect(screen.getByText("Auto-refresh OFF")).toBeInTheDocument();

    fireEvent.click(screen.getByText("Auto-refresh OFF"));

    expect(screen.getByText("Auto-refresh ON")).toBeInTheDocument();
  });

  it("auto-refreshes every 30 seconds when enabled", async () => {
    mockApi.get.mockResolvedValue(mockDashboardStats);

    render(<Analytics />);

    await waitFor(() => {
      expect(mockApi.get).toHaveBeenCalledTimes(1);
    });

    // Fast-forward 30 seconds
    jest.advanceTimersByTime(30000);

    await waitFor(() => {
      expect(mockApi.get).toHaveBeenCalledTimes(2);
    });

    // Fast-forward another 30 seconds
    jest.advanceTimersByTime(30000);

    await waitFor(() => {
      expect(mockApi.get).toHaveBeenCalledTimes(3);
    });
  });

  it("does not auto-refresh when disabled", async () => {
    mockApi.get.mockResolvedValue(mockDashboardStats);

    render(<Analytics />);

    await waitFor(() => {
      expect(mockApi.get).toHaveBeenCalled();
    });

    // Turn off auto-refresh
    const autoRefreshButton = screen.getByText("Auto-refresh ON");
    fireEvent.click(autoRefreshButton);

    // Get call count before advancing time
    const callCountBeforeAdvance = mockApi.get.mock.calls.length;

    // Fast-forward 30 seconds
    jest.advanceTimersByTime(30000);

    // Wait a bit and verify no new calls
    await new Promise(resolve => setTimeout(resolve, 100));

    // Should still be same call count (no auto-refresh)
    expect(mockApi.get.mock.calls.length).toBe(callCountBeforeAdvance);
  });

  it("displays error message when fetch fails", async () => {
    mockApi.get.mockRejectedValueOnce(new Error("Network error"));

    render(<Analytics />);

    await waitFor(() => {
      expect(screen.getByText("Error Loading Analytics")).toBeInTheDocument();
      expect(screen.getByText("Network error")).toBeInTheDocument();
    });
  });

  it("allows retrying after error", async () => {
    mockApi.get
      .mockRejectedValueOnce(new Error("Network error"))
      .mockResolvedValueOnce(mockDashboardStats);

    render(<Analytics />);

    await waitFor(() => {
      expect(screen.getByText("Network error")).toBeInTheDocument();
    });

    const retryButton = screen.getByText("Retry");
    fireEvent.click(retryButton);

    await waitFor(() => {
      expect(screen.getByText("150")).toBeInTheDocument();
    });
  });

  it("exports CSV when export CSV button clicked", async () => {
    mockApi.get.mockResolvedValue(mockDashboardStats);

    // Mock fetch for export
    global.fetch = jest.fn().mockResolvedValueOnce({
      ok: true,
      blob: async () => new Blob(["csv data"], { type: "text/csv" }),
    } as Response);

    // Mock DOM methods
    const createObjectURLMock = jest.fn(() => "blob:mock-url");
    const revokeObjectURLMock = jest.fn();
    global.URL.createObjectURL = createObjectURLMock;
    global.URL.revokeObjectURL = revokeObjectURLMock;

    const createElementSpy = jest.spyOn(document, "createElement");
    const appendChildSpy = jest.spyOn(document.body, "appendChild");
    const removeChildSpy = jest.spyOn(document.body, "removeChild");

    render(<Analytics />);

    await waitFor(() => {
      expect(screen.getByText("150")).toBeInTheDocument();
    });

    const exportCsvButton = screen.getByText("Export CSV");
    fireEvent.click(exportCsvButton);

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining("/admin/analytics/export"),
        expect.objectContaining({
          method: "POST",
          body: expect.stringContaining('"format":"csv"'),
        })
      );
      expect(createObjectURLMock).toHaveBeenCalled();
      expect(appendChildSpy).toHaveBeenCalled();
      expect(removeChildSpy).toHaveBeenCalled();
    });

    createElementSpy.mockRestore();
    appendChildSpy.mockRestore();
    removeChildSpy.mockRestore();
  });

  it("exports JSON when export JSON button clicked", async () => {
    mockApi.get.mockResolvedValue(mockDashboardStats);

    // Mock fetch for export
    global.fetch = jest.fn().mockResolvedValueOnce({
      ok: true,
      blob: async () => new Blob(['{"test":"data"}'], { type: "application/json" }),
    } as Response);

    // Mock DOM methods
    global.URL.createObjectURL = jest.fn(() => "blob:mock-url");
    global.URL.revokeObjectURL = jest.fn();

    const createElementSpy = jest.spyOn(document, "createElement");
    const appendChildSpy = jest.spyOn(document.body, "appendChild");
    const removeChildSpy = jest.spyOn(document.body, "removeChild");

    render(<Analytics />);

    await waitFor(() => {
      expect(screen.getByText("150")).toBeInTheDocument();
    });

    const exportJsonButton = screen.getByText("Export JSON");
    fireEvent.click(exportJsonButton);

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining("/admin/analytics/export"),
        expect.objectContaining({
          method: "POST",
          body: expect.stringContaining('"format":"json"'),
        })
      );
    });

    createElementSpy.mockRestore();
    appendChildSpy.mockRestore();
    removeChildSpy.mockRestore();
  });

  it("shows toast notification on export success", async () => {
    mockApi.get.mockResolvedValue(mockDashboardStats);

    global.fetch = jest.fn().mockResolvedValueOnce({
      ok: true,
      blob: async () => new Blob(["data"], { type: "text/csv" }),
    } as Response);

    global.URL.createObjectURL = jest.fn(() => "blob:mock-url");
    global.URL.revokeObjectURL = jest.fn();

    const mockAnchor = document.createElement("a");
    const createElementSpy = jest.spyOn(document, "createElement").mockReturnValue(mockAnchor);
    const appendChildSpy = jest.spyOn(document.body, "appendChild").mockImplementation(() => mockAnchor);
    const removeChildSpy = jest.spyOn(document.body, "removeChild").mockImplementation(() => mockAnchor);

    render(<Analytics />);

    await waitFor(() => {
      expect(screen.getByText("150")).toBeInTheDocument();
    });

    const exportButton = screen.getByText("Export CSV");
    fireEvent.click(exportButton);

    await waitFor(() => {
      expect(screen.getByText("Analytics exported as CSV")).toBeInTheDocument();
    }, { timeout: 3000 });

    // Cleanup
    createElementSpy.mockRestore();
    appendChildSpy.mockRestore();
    removeChildSpy.mockRestore();
  });

  it("displays last update timestamp", async () => {
    mockApi.get.mockResolvedValue(mockDashboardStats);

    render(<Analytics />);

    // Wait for data to load first
    await waitFor(() => {
      expect(screen.getByText("150")).toBeInTheDocument();
    });

    // Last updated text should be visible after data loads
    await waitFor(() => {
      expect(screen.getByText(/Last updated:/)).toBeInTheDocument();
    }, { timeout: 3000 });
  });
});
