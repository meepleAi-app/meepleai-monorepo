import { render, screen, fireEvent } from "@testing-library/react";
import ViewModeToggle from "../ViewModeToggle";

describe("ViewModeToggle", () => {
  const mockOnModeChange = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("renders both mode buttons", () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    expect(screen.getByText(/📝 Editor Visuale/)).toBeInTheDocument();
    expect(screen.getByText(/\{ \} Codice JSON/)).toBeInTheDocument();
  });

  it("highlights rich mode when active", () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    expect(richButton).toHaveStyle({
      background: "white",
      color: "#0070f3",
      fontWeight: "bold"
    });
  });

  it("highlights json mode when active", () => {
    render(<ViewModeToggle mode="json" onModeChange={mockOnModeChange} />);

    const jsonButton = screen.getByText(/\{ \} Codice JSON/);
    expect(jsonButton).toHaveStyle({
      background: "white",
      color: "#0070f3",
      fontWeight: "bold"
    });
  });

  it("does not highlight inactive rich mode", () => {
    render(<ViewModeToggle mode="json" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    expect(richButton).toHaveStyle({
      background: "transparent",
      color: "#666",
      fontWeight: "normal"
    });
  });

  it("does not highlight inactive json mode", () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const jsonButton = screen.getByText(/\{ \} Codice JSON/);
    expect(jsonButton).toHaveStyle({
      background: "transparent",
      color: "#666",
      fontWeight: "normal"
    });
  });

  it("calls onModeChange with 'rich' when rich button is clicked", () => {
    render(<ViewModeToggle mode="json" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    fireEvent.click(richButton);

    expect(mockOnModeChange).toHaveBeenCalledWith("rich");
    expect(mockOnModeChange).toHaveBeenCalledTimes(1);
  });

  it("calls onModeChange with 'json' when json button is clicked", () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const jsonButton = screen.getByText(/\{ \} Codice JSON/);
    fireEvent.click(jsonButton);

    expect(mockOnModeChange).toHaveBeenCalledWith("json");
    expect(mockOnModeChange).toHaveBeenCalledTimes(1);
  });

  it("allows clicking the same mode button (doesn't prevent redundant clicks)", () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    fireEvent.click(richButton);

    expect(mockOnModeChange).toHaveBeenCalledWith("rich");
  });

  it("displays tooltip titles for accessibility", () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    const jsonButton = screen.getByText(/\{ \} Codice JSON/);

    expect(richButton).toHaveAttribute("title", "Editor visuale con formattazione");
    expect(jsonButton).toHaveAttribute("title", "Visualizza e modifica JSON direttamente");
  });

  it("has proper container styling", () => {
    const { container } = render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const wrapper = container.firstChild as HTMLElement;
    expect(wrapper).toHaveStyle({
      display: "inline-flex",
      background: "#f0f0f0",
      borderRadius: "4px",
      padding: "2px"
    });
  });

  it("applies box shadow to active button", () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    expect(richButton).toHaveStyle({
      boxShadow: "0 1px 3px rgba(0,0,0,0.1)"
    });
  });

  it("does not apply box shadow to inactive button", () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const jsonButton = screen.getByText(/\{ \} Codice JSON/);
    expect(jsonButton).toHaveStyle({
      boxShadow: "none"
    });
  });
});
