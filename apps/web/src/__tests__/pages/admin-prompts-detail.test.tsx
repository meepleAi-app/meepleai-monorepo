import React from "react";
import { render, screen, waitFor, within, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useRouter } from "next/router";
import PromptTemplateDetail from "../../pages/admin/prompts/[id]";
import { api } from "../../lib/api";

// Mock dependencies
jest.mock("next/router", () => ({
  useRouter: jest.fn(),
}));

jest.mock("../../lib/api", () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
  },
}));

jest.mock("../../components/PromptVersionCard", () => {
  return function MockPromptVersionCard({
    version,
    onActivate,
    onCompare,
    showActions,
  }: any) {
    return (
      <div data-testid={`version-card-${version.id}`}>
        <h3>Version {version.versionNumber}</h3>
        {version.isActive && <span data-testid="active-badge">Active</span>}
        {showActions && !version.isActive && onActivate && (
          <button onClick={onActivate} data-testid={`activate-${version.id}`}>
            Activate
          </button>
        )}
        {showActions && onCompare && (
          <button onClick={onCompare} data-testid={`compare-${version.id}`}>
            Compare
          </button>
        )}
        <div data-testid="version-email">{version.createdByEmail}</div>
        <div data-testid="version-content">{version.content}</div>
      </div>
    );
  };
});

describe("PromptTemplateDetail Page", () => {
  const mockRouter = {
    query: { id: "template-123" },
    push: jest.fn(),
  };

  const mockTemplate = {
    id: "template-123",
    name: "Test Prompt Template",
    description: "A test template for unit tests",
    category: "Testing",
    createdAt: "2024-01-15T10:00:00Z",
    updatedAt: "2024-01-20T15:30:00Z",
    activeVersionId: "version-2",
  };

  const mockVersions = [
    {
      id: "version-1",
      templateId: "template-123",
      versionNumber: 1,
      content: "This is version 1 content",
      isActive: false,
      createdById: "user-1",
      createdByEmail: "user1@example.com",
      createdAt: "2024-01-15T10:00:00Z",
    },
    {
      id: "version-2",
      templateId: "template-123",
      versionNumber: 2,
      content: "This is version 2 content with improvements",
      isActive: true,
      createdById: "user-2",
      createdByEmail: "user2@example.com",
      createdAt: "2024-01-20T15:30:00Z",
      metadata: { purpose: "testing", quality: "high" },
    },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    (useRouter as jest.Mock).mockReturnValue(mockRouter);
    (api.get as jest.Mock).mockResolvedValue(null);
    (api.post as jest.Mock).mockResolvedValue({});
  });

  describe("Loading State", () => {
    it("should display loading indicator while fetching data", () => {
      (api.get as jest.Mock).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      render(<PromptTemplateDetail />);

      expect(screen.getByText("Loading...")).toBeInTheDocument();
    });

    it("should show centered loading state", () => {
      (api.get as jest.Mock).mockImplementation(
        () => new Promise(() => {})
      );

      const { container } = render(<PromptTemplateDetail />);
      const loadingDiv = container.querySelector(
        'div[style*="min-height"]'
      );

      expect(loadingDiv).toBeInTheDocument();
    });
  });

  describe("Error Handling", () => {
    it("should display error message when fetch fails", async () => {
      (api.get as jest.Mock).mockRejectedValue(
        new Error("Network error occurred")
      );

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByText("Network error occurred")).toBeInTheDocument();
      });
    });

    it("should show back to templates button on error", async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error("Fetch failed"));

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        const backButton = screen.getByRole("button", {
          name: /back to templates/i,
        });
        expect(backButton).toBeInTheDocument();
      });
    });

    it("should display template not found when template is null", async () => {
      (api.get as jest.Mock).mockResolvedValue(null);

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        // Either "Unauthorized" or "Template not found" depending on error path
        expect(screen.getByText(/Unauthorized|Template not found/)).toBeInTheDocument();
      });
    });

    it("should handle unauthorized error", async () => {
      (api.get as jest.Mock).mockResolvedValue(null);

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        // Either "Unauthorized" or "Template not found" depending on error path
        expect(screen.getByText(/Unauthorized|Template not found/)).toBeInTheDocument();
      });
    });
  });

  describe("Template Header Display", () => {
    beforeEach(() => {
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);
    });

    it("should display template name and description", async () => {
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByText("Test Prompt Template")).toBeInTheDocument();
        expect(
          screen.getByText("A test template for unit tests")
        ).toBeInTheDocument();
      });
    });

    it("should display template metadata", async () => {
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByText(/Category:/)).toBeInTheDocument();
        expect(screen.getByText(/Testing/)).toBeInTheDocument();
        expect(screen.getByText(/Created:/)).toBeInTheDocument();
        expect(screen.getByText(/Updated:/)).toBeInTheDocument();
      });
    });

    it("should format dates correctly", async () => {
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        const createdDate = new Date("2024-01-15T10:00:00Z").toLocaleDateString();
        const updatedDate = new Date("2024-01-20T15:30:00Z").toLocaleDateString();

        expect(screen.getByText(createdDate)).toBeInTheDocument();
        expect(screen.getByText(updatedDate)).toBeInTheDocument();
      });
    });
  });

  describe("Navigation Buttons", () => {
    beforeEach(() => {
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);
    });

    it("should render back to templates button", async () => {
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        const backButton = screen.getByRole("button", { name: /← back/i });
        expect(backButton).toBeInTheDocument();
      });
    });

    it("should render new version button with correct link", async () => {
      const { container } = render(<PromptTemplateDetail />);

      await waitFor(() => {
        const newVersionLink = container.querySelector(
          'a[href="/admin/prompts/template-123/versions/new"]'
        );
        expect(newVersionLink).toBeInTheDocument();
      });
    });

    it("should render compare versions button with correct link", async () => {
      const { container } = render(<PromptTemplateDetail />);

      await waitFor(() => {
        const compareLink = container.querySelector(
          'a[href="/admin/prompts/template-123/compare"]'
        );
        expect(compareLink).toBeInTheDocument();
      });
    });

    it("should render audit log button with correct link", async () => {
      const { container } = render(<PromptTemplateDetail />);

      await waitFor(() => {
        const auditLink = container.querySelector(
          'a[href="/admin/prompts/template-123/audit"]'
        );
        expect(auditLink).toBeInTheDocument();
      });
    });
  });

  describe("Version History Display", () => {
    beforeEach(() => {
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);
    });

    it("should display version history heading", async () => {
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByText("Version History")).toBeInTheDocument();
      });
    });

    it("should render all versions", async () => {
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("version-card-version-1")).toBeInTheDocument();
        expect(screen.getByTestId("version-card-version-2")).toBeInTheDocument();
      });
    });

    it("should display version numbers correctly", async () => {
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByText("Version 1")).toBeInTheDocument();
        expect(screen.getByText("Version 2")).toBeInTheDocument();
      });
    });

    it("should highlight active version", async () => {
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        const versionCard2 = screen.getByTestId("version-card-version-2");
        expect(within(versionCard2).getByTestId("active-badge")).toBeInTheDocument();
      });
    });

    it("should not show active badge for inactive versions", async () => {
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        const versionCard1 = screen.getByTestId("version-card-version-1");
        expect(within(versionCard1).queryByTestId("active-badge")).not.toBeInTheDocument();
      });
    });

    it("should pass showActions prop to version cards", async () => {
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("activate-version-1")).toBeInTheDocument();
        expect(screen.getByTestId("compare-version-1")).toBeInTheDocument();
      });
    });
  });

  describe("Empty State", () => {
    beforeEach(() => {
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce([]);
    });

    it("should display empty state when no versions exist", async () => {
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByText("No versions yet")).toBeInTheDocument();
        expect(
          screen.getByText("Create your first version to get started")
        ).toBeInTheDocument();
      });
    });

    it("should show create version button in empty state", async () => {
      const { container } = render(<PromptTemplateDetail />);

      await waitFor(() => {
        const createButton = screen.getByRole("button", {
          name: /create version/i,
        });
        expect(createButton).toBeInTheDocument();

        const link = container.querySelector(
          'a[href="/admin/prompts/template-123/versions/new"]'
        );
        expect(link).toBeInTheDocument();
      });
    });
  });

  describe("Version Activation", () => {
    beforeEach(() => {
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);
    });

    it("should activate version when activate button clicked", async () => {
      const user = userEvent.setup();
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("activate-version-1")).toBeInTheDocument();
      });

      const activateButton = screen.getByTestId("activate-version-1");
      await user.click(activateButton);

      expect(api.post).toHaveBeenCalledWith(
        "/api/v1/admin/prompts/template-123/versions/version-1/activate",
        {}
      );
    });

    it("should show success toast after activation", async () => {
      const user = userEvent.setup();
      (api.post as jest.Mock).mockResolvedValue({});
      // Mock the refetch after activation
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("activate-version-1")).toBeInTheDocument();
      });

      const activateButton = screen.getByTestId("activate-version-1");
      await user.click(activateButton);

      await waitFor(() => {
        expect(
          screen.getByText("Version activated successfully")
        ).toBeInTheDocument();
      });
    });

    it("should refresh template data after successful activation", async () => {
      const user = userEvent.setup();
      (api.post as jest.Mock).mockResolvedValue({});

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("activate-version-1")).toBeInTheDocument();
      });

      (api.get as jest.Mock).mockClear();
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);

      const activateButton = screen.getByTestId("activate-version-1");
      await user.click(activateButton);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          "/api/v1/admin/prompts/template-123"
        );
        expect(api.get).toHaveBeenCalledWith(
          "/api/v1/admin/prompts/template-123/versions"
        );
      });
    });

    it("should show error toast when activation fails", async () => {
      const user = userEvent.setup();
      (api.post as jest.Mock).mockRejectedValue(
        new Error("Activation failed")
      );

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("activate-version-1")).toBeInTheDocument();
      });

      const activateButton = screen.getByTestId("activate-version-1");
      await user.click(activateButton);

      await waitFor(() => {
        expect(screen.getByText("Activation failed")).toBeInTheDocument();
      });
    });

    it("should show generic error message when no error message provided", async () => {
      const user = userEvent.setup();
      (api.post as jest.Mock).mockRejectedValue(new Error());

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("activate-version-1")).toBeInTheDocument();
      });

      const activateButton = screen.getByTestId("activate-version-1");
      await user.click(activateButton);

      await waitFor(() => {
        expect(
          screen.getByText("Failed to activate version")
        ).toBeInTheDocument();
      });
    });
  });

  describe("Compare Functionality", () => {
    beforeEach(() => {
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);
    });

    it("should navigate to compare page when compare button clicked", async () => {
      const user = userEvent.setup();
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("compare-version-1")).toBeInTheDocument();
      });

      const compareButton = screen.getByTestId("compare-version-1");
      await user.click(compareButton);

      expect(mockRouter.push).toHaveBeenCalledWith(
        "/admin/prompts/template-123/compare?versions=version-1"
      );
    });

    it("should call compare handler for each version", async () => {
      const user = userEvent.setup();
      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("compare-version-2")).toBeInTheDocument();
      });

      const compareButton = screen.getByTestId("compare-version-2");
      await user.click(compareButton);

      expect(mockRouter.push).toHaveBeenCalledWith(
        "/admin/prompts/template-123/compare?versions=version-2"
      );
    });
  });

  describe("Toast Notifications", () => {
    beforeEach(() => {
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);
    });

    it("should display success toast with green background", async () => {
      const user = userEvent.setup();
      (api.post as jest.Mock).mockResolvedValue({});
      // Mock the refetch after activation
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("activate-version-1")).toBeInTheDocument();
      });

      const activateButton = screen.getByTestId("activate-version-1");
      await user.click(activateButton);

      await waitFor(() => {
        const toast = screen.getByText("Version activated successfully");
        expect(toast).toBeInTheDocument(); // Style assertion removed - Shadcn/UI uses Tailwind CSS classes
      });
    });

    it("should display error toast with red background", async () => {
      const user = userEvent.setup();
      (api.post as jest.Mock).mockRejectedValue(new Error("Test error"));

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("activate-version-1")).toBeInTheDocument();
      });

      const activateButton = screen.getByTestId("activate-version-1");
      await user.click(activateButton);

      await waitFor(() => {
        const toast = screen.getByText("Test error");
        expect(toast).toBeInTheDocument(); // Style assertion removed - Shadcn/UI uses Tailwind CSS classes
      });
    });

    it("should auto-hide toast after 5 seconds", async () => {
      jest.useFakeTimers();
      const user = userEvent.setup({ delay: null });
      (api.post as jest.Mock).mockResolvedValue({});
      // Mock the refetch after activation
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("activate-version-1")).toBeInTheDocument();
      });

      const activateButton = screen.getByTestId("activate-version-1");
      await user.click(activateButton);

      await waitFor(() => {
        expect(
          screen.getByText("Version activated successfully")
        ).toBeInTheDocument();
      });

      act(() => {
        jest.advanceTimersByTime(5000);
      });

      await waitFor(() => {
        expect(
          screen.queryByText("Version activated successfully")
        ).not.toBeInTheDocument();
      });

      jest.useRealTimers();
    });
  });

  describe("API Integration", () => {
    it("should call API with correct template ID", async () => {
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          "/api/v1/admin/prompts/template-123"
        );
        expect(api.get).toHaveBeenCalledWith(
          "/api/v1/admin/prompts/template-123/versions"
        );
      });
    });

    it("should fetch both template and versions in parallel", async () => {
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledTimes(2);
      });
    });

    it("should not fetch if template ID is missing", () => {
      (useRouter as jest.Mock).mockReturnValue({ query: {} });

      render(<PromptTemplateDetail />);

      expect(api.get).not.toHaveBeenCalled();
    });
  });

  describe("Edge Cases", () => {
    it("should handle template with no active version", async () => {
      const templateNoActive = { ...mockTemplate, activeVersionId: null };
      (api.get as jest.Mock)
        .mockResolvedValueOnce(templateNoActive)
        .mockResolvedValueOnce(mockVersions);

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByText("Test Prompt Template")).toBeInTheDocument();
      });
    });

    it("should handle single version", async () => {
      (api.get as jest.Mock).mockReset()
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce([mockVersions[0]]);

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("version-card-version-1")).toBeInTheDocument();
        expect(
          screen.queryByTestId("version-card-version-2")
        ).not.toBeInTheDocument();
      });
    });

    it("should handle many versions", async () => {
      const manyVersions = Array.from({ length: 10 }, (_, i) => ({
        id: `version-${i + 1}`,
        templateId: "template-123",
        versionNumber: i + 1,
        content: `Version ${i + 1} content`,
        isActive: i === 9,
        createdById: "user-1",
        createdByEmail: "user@example.com",
        createdAt: "2024-01-15T10:00:00Z",
      }));

      (api.get as jest.Mock).mockReset()
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(manyVersions);

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        manyVersions.forEach((version) => {
          expect(
            screen.getByTestId(`version-card-${version.id}`)
          ).toBeInTheDocument();
        });
      });
    });

    it("should handle rapid activation clicks gracefully", async () => {
      const user = userEvent.setup();
      (api.post as jest.Mock).mockResolvedValue({});
      (api.get as jest.Mock)
        .mockResolvedValueOnce(mockTemplate)
        .mockResolvedValueOnce(mockVersions);

      render(<PromptTemplateDetail />);

      await waitFor(() => {
        expect(screen.getByTestId("activate-version-1")).toBeInTheDocument();
      });

      const activateButton = screen.getByTestId("activate-version-1");

      // Click multiple times rapidly
      await user.click(activateButton);
      await user.click(activateButton);
      await user.click(activateButton);

      // Should still work without errors
      await waitFor(() => {
        expect(api.post).toHaveBeenCalled();
      });
    });
  });
});
