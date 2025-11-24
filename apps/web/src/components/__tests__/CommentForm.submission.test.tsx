import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CommentForm } from "../CommentForm";

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
    let consoleErrorSpy: SpyInstance;
    let alertSpy: SpyInstance;

    beforeEach(() => {
      consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
      alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
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