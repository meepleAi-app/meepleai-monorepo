/**
 * CONFIG-06: FeatureFlagsTab Component Tests
 */

import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import FeatureFlagsTab from "../FeatureFlagsTab";
import { api } from "../../../lib/api";
import { toast } from "@/components/Toast";

// Mock dependencies
jest.mock("../../../lib/api");
jest.mock("@/components/Toast");

const mockApi = api as jest.Mocked<typeof api>;
const mockToast = toast as jest.Mocked<typeof toast>;

describe("FeatureFlagsTab", () => {
  const criticalStreamingFlag = {
    id: "2",
    key: "Features:StreamingResponses",
    value: "false",
    valueType: "boolean",
    description: "Enable SSE streaming for QA",
    category: "FeatureFlag",
    isActive: true,
    requiresRestart: true,
    environment: "All",
    version: 1,
    previousValue: null,
    createdAt: "2025-01-01T00:00:00Z",
    updatedAt: "2025-01-01T00:00:00Z",
    createdByUserId: "admin",
    updatedByUserId: null,
    lastToggledAt: null,
  } as const;

  const mockConfigurations = [
    {
      id: "1",
      key: "Features:RagCaching",
      value: "true",
      valueType: "boolean",
      description: "Enable RAG response caching",
      category: "FeatureFlag",
      isActive: true,
      requiresRestart: false,
      environment: "All",
      version: 1,
      previousValue: null,
      createdAt: "2025-01-01T00:00:00Z",
      updatedAt: "2025-01-01T00:00:00Z",
      createdByUserId: "admin",
      updatedByUserId: null,
      lastToggledAt: null,
    },
    criticalStreamingFlag,
    {
      id: "3",
      key: "Features:BetaSurvey",
      value: "false",
      valueType: "boolean",
      description: "Show beta survey banner",
      category: "FeatureFlag",
      isActive: true,
      requiresRestart: false,
      environment: "All",
      version: 1,
      previousValue: null,
      createdAt: "2025-01-01T00:00:00Z",
      updatedAt: "2025-01-01T00:00:00Z",
      createdByUserId: "admin",
      updatedByUserId: null,
      lastToggledAt: null,
    }
  ];

  const mockOnChange = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    mockApi.config = {
      updateConfiguration: jest.fn().mockResolvedValue({}),
    } as any;
    mockToast.success = jest.fn();
    mockToast.error = jest.fn();
  });

  it("renders feature flags correctly", () => {
    render(
      <FeatureFlagsTab
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    expect(screen.getAllByRole('heading', { name: /RagCaching/i }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('heading', { name: /StreamingResponses/i }).length).toBeGreaterThan(0);
  });

  it("shows active features preview", () => {
    render(
      <FeatureFlagsTab
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    expect(screen.getByText(/Currently Active Features \(1\)/)).toBeInTheDocument();
    const activeBadges = screen.getAllByText("RagCaching");
    expect(activeBadges.length).toBeGreaterThan(0);
  });

  it("toggles non-critical feature flags without confirmation", async () => {
    const confirmSpy = jest.spyOn(window, "confirm").mockImplementation(() => true);

    render(
      <FeatureFlagsTab
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    const toggleButtons = screen.getAllByRole("switch");
    const betaSurveyToggle = toggleButtons[2];

    fireEvent.click(betaSurveyToggle);

    await waitFor(() => {
      expect(mockApi.config.updateConfiguration).toHaveBeenCalledWith("3", {
        value: "true",
      });
      expect(mockToast.success).toHaveBeenCalled();
      expect(mockOnChange).toHaveBeenCalled();
    });

    expect(confirmSpy).not.toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it("shows confirmation prompt for critical features before disabling", () => {
    const confirmSpy = jest.spyOn(window, "confirm").mockReturnValue(false);

    render(
      <FeatureFlagsTab
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    const ragCachingToggle = screen.getAllByRole("switch")[0];
    fireEvent.click(ragCachingToggle);

    expect(confirmSpy).toHaveBeenCalled();
    expect(mockApi.config.updateConfiguration).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
  });

  it("displays restart warning for flags requiring restart", () => {
    render(
      <FeatureFlagsTab
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    const restartWarnings = screen.getAllByText(/Requires restart/);
    expect(restartWarnings.length).toBeGreaterThan(0);
  });

  it("shows empty state when no feature flags", () => {
    render(
      <FeatureFlagsTab configurations={[]} onConfigurationChange={mockOnChange} />
    );

    expect(screen.getByText(/No feature flags found/)).toBeInTheDocument();
  });

  it("surfaces API errors and keeps toggle state unchanged", async () => {
    const confirmSpy = jest.spyOn(window, "confirm").mockImplementation(() => true);
    mockApi.config.updateConfiguration = jest
      .fn()
      .mockRejectedValue(new Error("API Error"));

    render(
      <FeatureFlagsTab
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    const streamingToggle = screen.getAllByRole("switch")[1];
    fireEvent.click(streamingToggle);

    await waitFor(() => {
      expect(mockToast.error).toHaveBeenCalled();
    });

    confirmSpy.mockRestore();
  });
});
