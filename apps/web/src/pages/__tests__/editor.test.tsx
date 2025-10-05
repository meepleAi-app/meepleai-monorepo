import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import RuleSpecEditor from "../editor";
import { useRouter } from "next/router";
import { api } from "../../lib/api";

jest.mock("next/router", () => ({
  useRouter: jest.fn()
}));

jest.mock("../../lib/api", () => ({
  api: {
    get: jest.fn(),
    put: jest.fn(),
    post: jest.fn()
  }
}));

const mockUseRouter = useRouter as jest.MockedFunction<typeof useRouter>;
const mockApi = api as jest.Mocked<typeof api>;
let consoleErrorSpy: jest.SpyInstance;

function createRouter(query: Record<string, unknown>): any {
  return {
    query,
    pathname: "/editor",
    asPath: "/editor",
    basePath: "",
    push: jest.fn(),
    replace: jest.fn(),
    reload: jest.fn(),
    back: jest.fn(),
    prefetch: jest.fn(),
    beforePopState: jest.fn(),
    events: {
      on: jest.fn(),
      off: jest.fn(),
      emit: jest.fn()
    },
    isFallback: false,
    isLocaleDomain: false,
    isReady: true,
    isPreview: false
  };
}

const baseSpec = {
  gameId: "game-1",
  version: "1.0",
  createdAt: "2024-01-01T00:00:00.000Z",
  rules: [
    {
      id: "rule-1",
      text: "First rule"
    }
  ]
};

beforeEach(() => {
  jest.clearAllMocks();
  consoleErrorSpy = jest.spyOn(console, "error").mockImplementation(() => {});
  mockUseRouter.mockReturnValue(createRouter({ gameId: "game-1" }) as any);
});

afterEach(() => {
  consoleErrorSpy.mockRestore();
});

it("renders login prompt when user is not authenticated", async () => {
  mockApi.get.mockResolvedValueOnce(null);

  render(<RuleSpecEditor />);

  await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith("/auth/me"));

  expect(screen.getByText(/Devi effettuare l'accesso/i)).toBeInTheDocument();
  expect(screen.getByRole("link", { name: /Torna alla home/i })).toBeInTheDocument();
});

it("renders unauthorized message for users without editor roles", async () => {
  mockApi.get
    .mockResolvedValueOnce({
      user: {
        id: "user-1",
        email: "viewer@example.com",
        role: "Viewer"
      },
      expiresAt: "2024-01-01T00:00:00.000Z"
    })
    .mockResolvedValueOnce(baseSpec);

  render(<RuleSpecEditor />);

  await waitFor(() =>
    expect(screen.getByText(/Non hai i permessi necessari per utilizzare l'editor/i)).toBeInTheDocument()
  );
});

it("initializes history and toggles undo\/redo buttons after edits", async () => {
  mockApi.get
    .mockResolvedValueOnce({
      user: {
        id: "user-2",
        email: "admin@example.com",
        role: "Admin"
      },
      expiresAt: "2024-01-01T00:00:00.000Z"
    })
    .mockResolvedValueOnce(baseSpec);

  render(<RuleSpecEditor />);

  await screen.findByRole("button", { name: /Annulla/i });
  await screen.findByRole("button", { name: /Ripeti/i });
  const saveButton = await screen.findByRole("button", { name: /Salva/i });
  expect(saveButton).not.toBeDisabled();

  const textarea = screen.getByRole("textbox") as HTMLTextAreaElement;

  expect(screen.getByRole("button", { name: /Annulla/i })).toBeDisabled();
  expect(screen.getByRole("button", { name: /Ripeti/i })).toBeDisabled();

  const updatedSpec = { ...baseSpec, version: "1.1" };
  const updatedJson = JSON.stringify(updatedSpec, null, 2);

  fireEvent.focus(textarea);
  fireEvent.change(textarea, { target: { value: updatedJson } });
  await waitFor(() => expect(textarea).toHaveValue(updatedJson));
  fireEvent.blur(textarea);

  await waitFor(() =>
    expect(screen.getByRole("button", { name: /Annulla/i })).not.toBeDisabled()
  );
  expect(screen.getByRole("button", { name: /Ripeti/i })).toBeDisabled();

  fireEvent.click(screen.getByRole("button", { name: /Annulla/i }));

  await waitFor(() => expect(textarea).toHaveValue(JSON.stringify(baseSpec, null, 2)));
  await waitFor(() =>
    expect(screen.getByRole("button", { name: /Ripeti/i })).not.toBeDisabled()
  );
  expect(screen.getByRole("button", { name: /Annulla/i })).toBeDisabled();
});

it("shows validation errors and success status message during save", async () => {
  mockApi.get
    .mockResolvedValueOnce({
      user: {
        id: "user-3",
        email: "editor@example.com",
        role: "Editor"
      },
      expiresAt: "2024-01-01T00:00:00.000Z"
    })
    .mockResolvedValueOnce(baseSpec);

  render(<RuleSpecEditor />);

  const textarea = await screen.findByRole("textbox");
  const saveButton = await screen.findByRole("button", { name: /Salva/i });

  await act(async () => {
    fireEvent.focus(textarea);
    fireEvent.change(textarea, { target: { value: "{" } });
    fireEvent.blur(textarea);
  });
  await waitFor(() => expect(textarea).toHaveValue("{"));

  await waitFor(() =>
    expect(
      screen.getByText((content) => content.includes("Expected property name"))
    ).toBeInTheDocument()
  );
  expect(saveButton).toBeDisabled();

  const specToPersist = { ...baseSpec, version: "2.0" };
  const specJson = JSON.stringify(specToPersist, null, 2);
  mockApi.put.mockRejectedValueOnce(new Error("API error"));
  mockApi.put.mockResolvedValueOnce(specToPersist);

  await act(async () => {
    fireEvent.change(textarea, { target: { value: specJson } });
  });
  await waitFor(() => expect(textarea).toHaveValue(specJson));
  await waitFor(() => expect(screen.getByText(/✓ JSON valido/i)).toBeInTheDocument());
  await waitFor(() => expect(saveButton).not.toBeDisabled());

  fireEvent.click(saveButton);

  await waitFor(() =>
    expect(screen.getByText(/API error/i)).toBeInTheDocument()
  );
  expect(mockApi.put).toHaveBeenCalledWith("/games/game-1/rulespec", specToPersist);

  fireEvent.click(saveButton);

  await waitFor(() =>
    expect(screen.getByText(/RuleSpec salvato con successo \(versione 2\.0\)/i)).toBeInTheDocument()
  );
  await waitFor(() =>
    expect(screen.queryByText(/API error/i)).not.toBeInTheDocument()
  );
});
