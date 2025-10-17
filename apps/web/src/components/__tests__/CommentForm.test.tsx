import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CommentForm } from "../CommentForm";

describe("CommentForm", () => {
  let user: ReturnType<typeof userEvent.setup>;
  let mockOnSubmit: jest.Mock;

  beforeEach(() => {
    user = userEvent.setup();
    mockOnSubmit = jest.fn().mockResolvedValue(undefined);
  });

  afterEach(() => {
    jest.clearAllMocks();
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

      expect(submitButton).toHaveStyle({ cursor: "not-allowed" });
    });

    it("applies correct cursor style when button is enabled", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "Valid comment");

      expect(submitButton).toHaveStyle({ cursor: "pointer" });
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
      mockOnSubmit.mockImplementation(
        () => new Promise((resolve) => setTimeout(resolve, 100))
      );

      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");

      await user.type(textarea, "Test comment");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.click(submitButton);
      await user.click(submitButton);
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockOnSubmit).toHaveBeenCalledTimes(1);
      });
    });
  });

  describe("error handling", () => {
    let consoleErrorSpy: jest.SpyInstance;
    let alertSpy: jest.SpyInstance;

    beforeEach(() => {
      consoleErrorSpy = jest.spyOn(console, "error").mockImplementation(() => {});
      alertSpy = jest.spyOn(window, "alert").mockImplementation(() => {});
    });

    afterEach(() => {
      consoleErrorSpy.mockRestore();
      alertSpy.mockRestore();
    });

    it("shows error alert when submission fails", async () => {
      mockOnSubmit.mockRejectedValue(new Error("Network error"));

      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "Test comment");
      await user.click(submitButton);

      await waitFor(() => {
        expect(alertSpy).toHaveBeenCalledWith("Impossibile creare il commento");
      });
    });

    it("logs error to console when submission fails", async () => {
      const error = new Error("Network error");
      mockOnSubmit.mockRejectedValue(error);

      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "Test comment");
      await user.click(submitButton);

      await waitFor(() => {
        expect(consoleErrorSpy).toHaveBeenCalledWith(
          "Failed to create comment:",
          error
        );
      });
    });

    it("does not clear textarea when submission fails", async () => {
      mockOnSubmit.mockRejectedValue(new Error("Network error"));

      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox") as HTMLTextAreaElement;
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "Test comment");
      await user.click(submitButton);

      await waitFor(() => {
        expect(alertSpy).toHaveBeenCalled();
      });

      expect(textarea.value).toBe("Test comment");
    });

    it("re-enables textarea and button after submission error", async () => {
      mockOnSubmit.mockRejectedValue(new Error("Network error"));

      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.type(textarea, "Test comment");
      await user.click(submitButton);

      await waitFor(() => {
        expect(textarea).not.toBeDisabled();
      });

      expect(submitButton).not.toBeDisabled();
    });
  });

  describe("edge cases", () => {
    it("handles special characters in comment text", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      const specialText = "Test <>&\"' @#$%^&*() 测试";
      await user.type(textarea, specialText);
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockOnSubmit).toHaveBeenCalledWith(specialText, null);
      });
    });

    it("handles very long comment text", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      const longText = "a".repeat(1000);
      // Use paste instead of type for long text to avoid timeout
      await user.click(textarea);
      await user.paste(longText);
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockOnSubmit).toHaveBeenCalledWith(longText, null);
      });
    });

    it("handles newlines in comment text", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      const textWithNewlines = "Line 1\nLine 2\nLine 3";
      // Use paste to avoid userEvent typing issues with newlines
      await user.click(textarea);
      await user.paste(textWithNewlines);
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockOnSubmit).toHaveBeenCalledWith(textWithNewlines, null);
      });
    });

    it("handles atomId as null explicitly", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} atomId={null} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.click(textarea);
      await user.paste("Test comment");
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockOnSubmit).toHaveBeenCalledWith("Test comment", null);
      });
    });

    it("handles atomId as undefined", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} atomId={undefined} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.click(textarea);
      await user.paste("Test comment");
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockOnSubmit).toHaveBeenCalledWith("Test comment", null);
      });
    });
  });

  describe("input changes", () => {
    it("updates textarea value on input", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox") as HTMLTextAreaElement;

      await user.click(textarea);
      await user.paste("Test");
      expect(textarea.value).toBe("Test");

      await user.paste(" comment");
      expect(textarea.value).toBe("Test comment");
    });

    it("enables button as user types valid text", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      expect(submitButton).toBeDisabled();

      await user.type(textarea, "T");
      expect(submitButton).not.toBeDisabled();

      await user.type(textarea, "est");
      expect(submitButton).not.toBeDisabled();
    });

    it("updates button state when clearing text", async () => {
      render(<CommentForm onSubmit={mockOnSubmit} />);

      const textarea = screen.getByRole("textbox");
      const submitButton = screen.getByRole("button", {
        name: "Aggiungi Commento"
      });

      await user.click(textarea);
      await user.paste("Test");

      await waitFor(() => {
        expect(submitButton).not.toBeDisabled();
      });

      await user.clear(textarea);

      await waitFor(() => {
        expect(submitButton).toBeDisabled();
      });
    });
  });
});
