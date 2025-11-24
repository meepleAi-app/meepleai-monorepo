import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CommentForm } from "../CommentForm";

describe("CommentForm", () => {
  let user: ReturnType<typeof userEvent.setup>;
  let mockOnSubmit: Mock;

  beforeEach(() => {
    user = userEvent.setup();
    mockOnSubmit = vi.fn().mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe("rendering", () => {
    it("renders textarea with default placeholder", () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      expect(
        screen.getByPlaceholderText("Scrivi un commento...")
      ).toBeInTheDocument();
    });

    it("renders textarea with custom placeholder", () => {
      render(
        <CommentForm
          onSubmit={mockOnSubmit}
          placeholder="Custom placeholder"
        />
      );

      expect(screen.getByPlaceholderText("Custom placeholder")).toBeInTheDocument();
    });

    it("renders submit button with default text", () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      expect(
        screen.getByRole("button", { name: "Aggiungi Commento" })
      ).toBeInTheDocument();
    });

    it("renders textarea and button", () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      expect(screen.getByRole("textbox")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Aggiungi Commento" })).toBeInTheDocument();
    });
  });

  describe("button states", () => {
    it("disables submit button when textarea is empty", () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });
      expect(submitButton).toBeDisabled();
    });

    it("disables submit button when textarea contains only whitespace", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "   ");

      expect(submitButton).toBeDisabled();
    });

    it("enables submit button when textarea has text", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "Valid comment");

      expect(submitButton).toBeEnabled();
    });

    it("applies correct cursor style when button is disabled", () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      // Check Tailwind class instead of inline style
      expect(submitButton).toHaveClass("cursor-not-allowed");
    });

    it("applies correct cursor style when button is enabled", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "Valid comment");

      // Check Tailwind class instead of inline style
      expect(submitButton).toHaveClass("cursor-pointer");
    });
  });

  describe("form submission", () => {
    it("calls onSubmit with comment text and null atomId by default", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "Test comment");
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockOnSubmit).toHaveBeenCalledWith("Test comment", null);
      });
    });

    it("calls onSubmit with comment text and provided atomId", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} atomId="atom-123" />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "Test comment");
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockOnSubmit).toHaveBeenCalledWith("Test comment", "atom-123");
      });
    });

    it("does not call onSubmit when textarea is empty", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      // Try to click (button should be disabled, but try anyway)
      await user.click(submitButton);

      expect(mockOnSubmit).not.toHaveBeenCalled();
    });

    it("does not call onSubmit when textarea contains only whitespace", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");

      await user.type(textarea, "   ");

      // Manually submit form
      const form = textarea.closest("form");
      if (form) {
        const submitEvent = new Event("submit", { bubbles: true, cancelable: true });
        form.dispatchEvent(submitEvent);
      }

      expect(mockOnSubmit).not.toHaveBeenCalled();
    });

    it("clears textarea after successful submission", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox") as HTMLTextAreaElement;
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "Test comment");
      await user.click(submitButton);

      await waitFor(() => {
        expect(textarea.value).toBe("");
      });
    });

    it("trims comment text before validation", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "  Comment with spaces  ");
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockOnSubmit).toHaveBeenCalledWith(
          "  Comment with spaces  ",
          null
        );
      });
    });
  });

  describe("loading states", () => {
    it("disables textarea during submission", async () => {
      mockOnSubmit.mockImplementation(
        () => new Promise((resolve) => setTimeout(resolve, 100))
      );

      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "Test comment");
      await user.click(submitButton);

      expect(textarea).toBeDisabled();
      expect(submitButton).toBeDisabled();

      await waitFor(() => {
        expect(textarea).not.toBeDisabled();
        expect(submitButton).toBeDisabled(); // Disabled because textarea is empty
      });
    });

    it("shows loading text during submission", async () => {
      mockOnSubmit.mockImplementation(
        () => new Promise((resolve) => setTimeout(resolve, 100))
      );

      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");

      await user.type(textarea, "Test comment");
      await user.click(screen.getByRole("button", { name: "Aggiungi Commento" }));

      expect(
        screen.getByRole("button", { name: "Invio in corso..." })
      ).toBeInTheDocument();

      await waitFor(() => {
        expect(
          screen.queryByRole("button", { name: "Invio in corso..." })
        ).not.toBeInTheDocument();
      });
    });

    it("prevents multiple submissions while submitting", async () => {
