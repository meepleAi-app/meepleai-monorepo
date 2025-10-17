import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ChessPage from "../../pages/chess";
import { api } from "../../lib/api";

// Mock the API
jest.mock("../../lib/api", () => ({
  api: {
    get: jest.fn(),
    post: jest.fn()
  }
}));

// Mock react-chessboard
jest.mock("react-chessboard", () => ({
  Chessboard: ({
    position,
    onPieceDrop,
    boardOrientation,
    customSquareStyles,
    boardWidth
  }: {
    position: string;
    onPieceDrop: (from: string, to: string) => boolean;
    boardOrientation: string;
    customSquareStyles: Record<string, object>;
    boardWidth: number;
  }) => (
    <div
      data-testid="chessboard"
      data-position={position}
      data-orientation={boardOrientation}
      data-board-width={boardWidth}
      data-custom-squares={JSON.stringify(customSquareStyles)}
    >
      <button
        data-testid="make-move-e2-e4"
        onClick={() => onPieceDrop("e2", "e4")}
      >
        Make Move e2-e4
      </button>
      <button
        data-testid="make-invalid-move"
        onClick={() => onPieceDrop("a1", "a1")}
      >
        Invalid Move
      </button>
    </div>
  )
}));

// Mock chess.js
jest.mock("chess.js", () => {
  const mockChessGame = {
    fen: jest.fn(() => "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"),
    turn: jest.fn(() => "w"),
    isCheckmate: jest.fn(() => false),
    isCheck: jest.fn(() => false),
    isDraw: jest.fn(() => false),
    isStalemate: jest.fn(() => false),
    move: jest.fn((move: unknown) => {
      const m = move as { from: string; to: string; promotion?: string };
      if (m.from === "e2" && m.to === "e4") {
        return { from: "e2", to: "e4", san: "e4" };
      }
      return null;
    })
  };

  return {
    Chess: jest.fn(() => mockChessGame)
  };
});

const mockApiGet = api.get as jest.Mock;
const mockApiPost = api.post as jest.Mock;

describe("ChessPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe("Authentication", () => {
    it("should show login required message when not authenticated", async () => {
      mockApiGet.mockResolvedValue(null);

      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByText("Accesso richiesto")).toBeInTheDocument();
      });

      expect(
        screen.getByText("Devi effettuare l'accesso per utilizzare la chat scacchi.")
      ).toBeInTheDocument();
      expect(screen.getByText("Vai al Login")).toBeInTheDocument();
    });

    it("should show chess interface when authenticated", async () => {
      mockApiGet.mockResolvedValue({
        user: {
          id: "user-1",
          email: "test@example.com",
          displayName: "Test User",
          role: "user"
        },
        expiresAt: "2025-12-31T23:59:59Z"
      });

      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByText("Chess Assistant")).toBeInTheDocument();
      });

      expect(screen.getByTestId("chessboard")).toBeInTheDocument();
      expect(screen.getByText("Chat con l'Agente")).toBeInTheDocument();
    });
  });

  describe("Chess Board", () => {
    beforeEach(async () => {
      mockApiGet.mockResolvedValue({
        user: {
          id: "user-1",
          email: "test@example.com",
          displayName: "Test User",
          role: "user"
        },
        expiresAt: "2025-12-31T23:59:59Z"
      });
    });

    it("should render chessboard with initial position", async () => {
      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByTestId("chessboard")).toBeInTheDocument();
      });

      const board = screen.getByTestId("chessboard");
      expect(board).toHaveAttribute(
        "data-position",
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
      );
    });

    it("should allow making valid moves", async () => {
      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByTestId("chessboard")).toBeInTheDocument();
      });

      const moveButton = screen.getByTestId("make-move-e2-e4");
      fireEvent.click(moveButton);

      await waitFor(() => {
        expect(screen.getByText(/Mossa eseguita: e4/i)).toBeInTheDocument();
      });
    });

    it("should reject invalid moves", async () => {
      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByTestId("chessboard")).toBeInTheDocument();
      });

      const invalidMoveButton = screen.getByTestId("make-invalid-move");
      fireEvent.click(invalidMoveButton);

      // Should not add any move message
      expect(screen.queryByText(/Mossa eseguita/i)).not.toBeInTheDocument();
    });

    it("should reset board when reset button is clicked", async () => {
      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByTestId("chessboard")).toBeInTheDocument();
      });

      // Make a move first
      const moveButton = screen.getByTestId("make-move-e2-e4");
      fireEvent.click(moveButton);

      await waitFor(() => {
        expect(screen.getByText(/Mossa eseguita/i)).toBeInTheDocument();
      });

      // Reset board
      const resetButton = screen.getByText("Nuova Partita");
      fireEvent.click(resetButton);

      // Messages should be cleared
      expect(screen.queryByText(/Mossa eseguita/i)).not.toBeInTheDocument();
    });

    it("should rotate board orientation", async () => {
      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByTestId("chessboard")).toBeInTheDocument();
      });

      const board = screen.getByTestId("chessboard");
      expect(board).toHaveAttribute("data-orientation", "white");

      const rotateButton = screen.getByText("Ruota Scacchiera");
      fireEvent.click(rotateButton);

      expect(board).toHaveAttribute("data-orientation", "black");

      fireEvent.click(rotateButton);
      expect(board).toHaveAttribute("data-orientation", "white");
    });

    it("should display game status", async () => {
      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByText("Turno:")).toBeInTheDocument();
      });

      expect(screen.getByText(/Bianco/)).toBeInTheDocument();
      expect(screen.getByText("Stato:")).toBeInTheDocument();
      expect(screen.getByText("FEN:")).toBeInTheDocument();
    });
  });

  describe("Chat Interface", () => {
    beforeEach(async () => {
      mockApiGet.mockResolvedValue({
        user: {
          id: "user-1",
          email: "test@example.com",
          displayName: "Test User",
          role: "user"
        },
        expiresAt: "2025-12-31T23:59:59Z"
      });
    });

    it("should show welcome message when no messages", async () => {
      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByText("Benvenuto nella Chess Chat!")).toBeInTheDocument();
      });

      expect(
        screen.getByText("Inizia facendo una domanda o muovi un pezzo sulla scacchiera.")
      ).toBeInTheDocument();
    });

    it("should send message to chess agent", async () => {
      const user = userEvent.setup();

      mockApiPost.mockResolvedValue({
        answer: "The best opening for white is 1.e4 (King's Pawn).",
        fen: null,
        suggestedMoves: ["e2e4"]
      });

      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByPlaceholderText(/Chiedi consigli/i)).toBeInTheDocument();
      });

      const input = screen.getByPlaceholderText(/Chiedi consigli/i);
      await user.type(input, "What is the best opening for white?");

      const sendButton = screen.getByText("Invia");
      await user.click(sendButton);

      await waitFor(() => {
        expect(mockApiPost).toHaveBeenCalledWith("/api/v1/agents/chess", {
          question: "What is the best opening for white?",
          fenPosition: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
        });
      });

      await waitFor(() => {
        expect(
          screen.getByText("The best opening for white is 1.e4 (King's Pawn).")
        ).toBeInTheDocument();
      });
    });

    it("should display suggested moves from AI", async () => {
      const user = userEvent.setup();

      mockApiPost.mockResolvedValue({
        answer: "I suggest the following moves:",
        fen: null,
        suggestedMoves: ["e2e4", "Nf3", "d2d4"]
      });

      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByPlaceholderText(/Chiedi consigli/i)).toBeInTheDocument();
      });

      const input = screen.getByPlaceholderText(/Chiedi consigli/i);
      await user.type(input, "Suggest next move");

      const sendButton = screen.getByText("Invia");
      await user.click(sendButton);

      await waitFor(() => {
        expect(screen.getByText("Mosse suggerite:")).toBeInTheDocument();
      });

      expect(screen.getByText("e2e4")).toBeInTheDocument();
      expect(screen.getByText("Nf3")).toBeInTheDocument();
      expect(screen.getByText("d2d4")).toBeInTheDocument();
    });

    it("should receive FEN position from AI response", async () => {
      const user = userEvent.setup();

      const newFen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1";

      mockApiPost.mockResolvedValue({
        answer: "Position updated after 1.e4",
        fen: newFen,
        suggestedMoves: []
      });

      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByPlaceholderText(/Chiedi consigli/i)).toBeInTheDocument();
      });

      const input = screen.getByPlaceholderText(/Chiedi consigli/i);
      await user.type(input, "Play e4");

      const sendButton = screen.getByText("Invia");
      await user.click(sendButton);

      await waitFor(() => {
        expect(screen.getByText("Position updated after 1.e4")).toBeInTheDocument();
      });

      // Verify the API was called
      expect(mockApiPost).toHaveBeenCalledWith("/api/v1/agents/chess", {
        question: "Play e4",
        fenPosition: expect.any(String)
      });
    });

    it("should handle API errors gracefully", async () => {
      const user = userEvent.setup();

      mockApiPost.mockRejectedValue(new Error("Network error"));

      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByPlaceholderText(/Chiedi consigli/i)).toBeInTheDocument();
      });

      const input = screen.getByPlaceholderText(/Chiedi consigli/i);
      await user.type(input, "Test question");

      const sendButton = screen.getByText("Invia");
      await user.click(sendButton);

      await waitFor(() => {
        expect(
          screen.getByText(/Errore nella comunicazione con l'agente scacchi/i)
        ).toBeInTheDocument();
      });

      // User message should be removed
      expect(screen.queryByText("Test question")).not.toBeInTheDocument();
    });

    it("should disable input while sending message", async () => {
      const user = userEvent.setup();

      mockApiPost.mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve({ answer: "Response" }), 100))
      );

      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByPlaceholderText(/Chiedi consigli/i)).toBeInTheDocument();
      });

      const input = screen.getByPlaceholderText(/Chiedi consigli/i);
      const sendButton = screen.getByText("Invia");

      await user.type(input, "Question");
      await user.click(sendButton);

      // Input should be disabled
      expect(input).toBeDisabled();
      expect(sendButton).toBeDisabled();
      expect(screen.getByText("Sto analizzando...")).toBeInTheDocument();

      await waitFor(() => {
        expect(input).not.toBeDisabled();
      });
    });

    it("should clear input after sending message", async () => {
      const user = userEvent.setup();

      mockApiPost.mockResolvedValue({
        answer: "Response",
        fen: null,
        suggestedMoves: []
      });

      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByPlaceholderText(/Chiedi consigli/i)).toBeInTheDocument();
      });

      const input = screen.getByPlaceholderText(/Chiedi consigli/i) as HTMLInputElement;
      await user.type(input, "Question");

      expect(input.value).toBe("Question");

      const sendButton = screen.getByText("Invia");
      await user.click(sendButton);

      await waitFor(() => {
        expect(input.value).toBe("");
      });
    });

    it("should prevent sending empty messages", async () => {
      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByPlaceholderText(/Chiedi consigli/i)).toBeInTheDocument();
      });

      const sendButton = screen.getByText("Invia");
      expect(sendButton).toBeDisabled();

      // Try to submit empty form
      fireEvent.submit(screen.getByPlaceholderText(/Chiedi consigli/i).closest("form")!);

      expect(mockApiPost).not.toHaveBeenCalled();
    });

    it("should display message timestamps", async () => {
      const user = userEvent.setup();

      mockApiPost.mockResolvedValue({
        answer: "Response",
        fen: null,
        suggestedMoves: []
      });

      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByPlaceholderText(/Chiedi consigli/i)).toBeInTheDocument();
      });

      const input = screen.getByPlaceholderText(/Chiedi consigli/i);
      await user.type(input, "Question");

      const sendButton = screen.getByText("Invia");
      await user.click(sendButton);

      await waitFor(() => {
        const timestamps = screen.getAllByText(/\d{1,2}:\d{2}:\d{2}/);
        expect(timestamps.length).toBeGreaterThan(0);
      });
    });
  });

  describe("Integration", () => {
    beforeEach(async () => {
      mockApiGet.mockResolvedValue({
        user: {
          id: "user-1",
          email: "test@example.com",
          displayName: "Test User",
          role: "user"
        },
        expiresAt: "2025-12-31T23:59:59Z"
      });
    });

    it("should send current FEN position with every question", async () => {
      const user = userEvent.setup();

      mockApiPost.mockResolvedValue({
        answer: "Analyzed",
        fen: null,
        suggestedMoves: []
      });

      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByPlaceholderText(/Chiedi consigli/i)).toBeInTheDocument();
      });

      // Make a move
      const moveButton = screen.getByTestId("make-move-e2-e4");
      fireEvent.click(moveButton);

      await waitFor(() => {
        expect(screen.getByText(/Mossa eseguita/i)).toBeInTheDocument();
      });

      // Ask a question
      const input = screen.getByPlaceholderText(/Chiedi consigli/i);
      await user.type(input, "Analyze this position");

      const sendButton = screen.getByText("Invia");
      await user.click(sendButton);

      await waitFor(() => {
        expect(mockApiPost).toHaveBeenCalledWith("/api/v1/agents/chess", {
          question: "Analyze this position",
          fenPosition: expect.any(String)
        });
      });
    });

    it("should have navigation link to home", async () => {
      render(<ChessPage />);

      await waitFor(() => {
        expect(screen.getByText("Home")).toBeInTheDocument();
      });

      const homeLink = screen.getByText("Home");
      expect(homeLink).toHaveAttribute("href", "/");
    });
  });
});
