/**
 * CONFIG-06: FeatureFlagsTab Component Tests
 */

import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import FeatureFlagsTab from "../FeatureFlagsTab";
import { api } from "../../../lib/api";
import { toast } from "react-hot-toast";

// Mock dependencies
jest.mock("../../../lib/api");
jest.mock("react-hot-toast");

const mockApi = api as jest.Mocked<typeof api>;
const mockToast = toast as jest.Mocked<typeof toast>;

describe("FeatureFlagsTab", () => {
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
    {
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
    },
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

    expect(screen.getByText(/RagCaching/)).toBeInTheDocument();
    expect(screen.getByText(/StreamingResponses/)).toBeInTheDocument();
  });

  it("shows active features preview", () => {
    render(
      <FeatureFlagsTab
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    expect(screen.getByText(/Currently Active Features \(1\)/)).toBeInTheDocument();
    expect(screen.getByText("RagCaching")).toBeInTheDocument();
  });

  it("toggles feature flag on click", async () => {
    render(
      <FeatureFlagsTab
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    const toggleButtons = screen.getAllByRole("switch");
    const ragCachingToggle = toggleButtons[0];

    fireEvent.click(ragCachingToggle);

    await waitFor(() => {
      expect(mockApi.config.updateConfiguration).toHaveBeenCalledWith("1", {
        value: "false",
      });
      expect(mockToast.success).toHaveBeenCalled();
      expect(mockOnChange).toHaveBeenCalled();
    });
  });

  it("shows confirmation for critical features", () => {
    const confirmSpy = jest.spyOn(window, "confirm").mockReturnValue(false);

    render(
      <FeatureFlagsTab
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    const toggleButtons = screen.getAllByRole("switch");
    fireEvent.click(toggleButtons[0]);

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

  it("handles API errors gracefully", async () => {
    mockApi.config.updateConfiguration = jest
      .fn()
      .mockRejectedValue(new Error("API Error"));

    render(
      <FeatureFlagsTab
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    const toggleButtons = screen.getAllByRole("switch");
    fireEvent.click(toggleButtons[1]); // Click non-critical flag

    await waitFor(() => {
      expect(mockToast.error).toHaveBeenCalled();
    });
  });
});
