/**
 * CONFIG-06: CategoryConfigTab Component Tests
 */

import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import CategoryConfigTab from "../CategoryConfigTab";
import { api } from "../../../lib/api";
import { toast } from "@/components/layout";

jest.mock("../../../lib/api");
jest.mock("@/components/layout");

const mockApi = api as jest.Mocked<typeof api>;
const mockToast = toast as jest.Mocked<typeof toast>;

describe("CategoryConfigTab", () => {
  const mockConfigurations = [
    {
      id: "1",
      key: "RateLimiting:RequestsPerMinute",
      value: "60",
      valueType: "integer",
      description: "Requests per minute limit",
      category: "RateLimiting",
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
    {
      id: "2",
      key: "Ai:Temperature",
      value: "0.7",
      valueType: "float",
      description: "AI temperature setting",
      category: "AiLlm",
      isActive: true,
      requiresRestart: false,
      environment: "All",
      version: 2,
      previousValue: "0.5",
      createdAt: "2025-01-01T00:00:00Z",
      updatedAt: "2025-01-02T00:00:00Z",
      createdByUserId: "admin",
      updatedByUserId: "admin",
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

  it("filters configurations by category", () => {
    render(
      <CategoryConfigTab
        title="Rate Limiting"
        category="RateLimiting"
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    expect(screen.getByText(/RequestsPerMinute/)).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: /Ai:Temperature/i })).not.toBeInTheDocument();
  });

  it("displays configuration metadata correctly", () => {
    render(
      <CategoryConfigTab
        title="AI/LLM"
        category="AiLlm"
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    expect(screen.getByRole("heading", { name: "Ai:Temperature" })).toBeInTheDocument();
    expect(screen.getAllByText(/Temperature/).length).toBeGreaterThan(0);
    expect(screen.getByText(/v2/)).toBeInTheDocument();
    expect(screen.getByText(/float/i)).toBeInTheDocument();
  });

  it("enables edit mode when edit button clicked", () => {
    render(
      <CategoryConfigTab
        title="Rate Limiting"
        category="RateLimiting"
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    const editButton = screen.getByText("Edit");
    fireEvent.click(editButton);

    expect(screen.getByText("Save")).toBeInTheDocument();
    expect(screen.getByText("Cancel")).toBeInTheDocument();
    expect(screen.getByDisplayValue("60")).toBeInTheDocument();
  });

  it("saves configuration value on save button click", async () => {
    render(
      <CategoryConfigTab
        title="Rate Limiting"
        category="RateLimiting"
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    const editButton = screen.getByText("Edit");
    fireEvent.click(editButton);

    const input = screen.getByDisplayValue("60");
    fireEvent.change(input, { target: { value: "100" } });

    const saveButton = screen.getByText("Save");
    fireEvent.click(saveButton);

    await waitFor(() => {
      expect(mockApi.config.updateConfiguration).toHaveBeenCalledWith("1", {
        value: "100",
      });
      expect(mockToast.success).toHaveBeenCalled();
      expect(mockOnChange).toHaveBeenCalled();
    });
  });

  it("shows destructive change warning for ChunkSize", () => {
    const chunkSizeConfig = {
      ...mockConfigurations[0],
      key: "Rag:ChunkSize",
      category: "Rag",
      value: "512",
    };

    const confirmSpy = jest.spyOn(window, "confirm").mockReturnValue(false);

    render(
      <CategoryConfigTab
        title="RAG"
        category="Rag"
        configurations={[chunkSizeConfig]}
        onConfigurationChange={mockOnChange}
      />
    );

    const editButton = screen.getByText("Edit");
    fireEvent.click(editButton);

    const input = screen.getByDisplayValue("512");
    fireEvent.change(input, { target: { value: "768" } });

    const saveButton = screen.getByText("Save");
    fireEvent.click(saveButton);

    expect(confirmSpy).toHaveBeenCalledWith(
      expect.stringContaining("re-indexing")
    );
    expect(mockApi.config.updateConfiguration).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
  });

  it("shows empty state when no configurations", () => {
    render(
      <CategoryConfigTab
        title="Test"
        category="Test"
        configurations={[]}
        onConfigurationChange={mockOnChange}
      />
    );

    expect(screen.getByText(/No Test configurations found/)).toBeInTheDocument();
  });

  it("displays restart warning badge", () => {
    render(
      <CategoryConfigTab
        title="Rate Limiting"
        category="RateLimiting"
        configurations={mockConfigurations}
        onConfigurationChange={mockOnChange}
      />
    );

    expect(screen.getByText(/Requires restart/)).toBeInTheDocument();
  });
});
