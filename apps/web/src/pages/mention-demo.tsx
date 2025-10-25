import { useState } from "react";
import { MentionInput } from "@/components/MentionInput";

/**
 * Demo page for testing MentionInput component (EDIT-05)
 * Navigate to /mention-demo to test @mention autocomplete functionality
 */
export default function MentionDemo() {
  const [commentText, setCommentText] = useState("");

  return (
    <div style={{ padding: 40, maxWidth: 800, margin: "0 auto" }}>
      <h1 style={{ fontSize: 24, marginBottom: 20 }}>MentionInput Component Demo</h1>

      <div style={{ marginBottom: 20, padding: 16, background: "#f0f9ff", border: "1px solid #bae6fd", borderRadius: 4 }}>
        <h2 style={{ fontSize: 16, marginBottom: 10, fontWeight: 600 }}>Instructions:</h2>
        <ul style={{ fontSize: 14, lineHeight: 1.6, marginLeft: 20 }}>
          <li>Type <code>@</code> followed by at least 2 characters to trigger user search</li>
          <li>Use <strong>Arrow Keys</strong> (↑↓) to navigate suggestions</li>
          <li>Press <strong>Enter</strong> to select highlighted user</li>
          <li>Press <strong>Escape</strong> to close dropdown</li>
          <li>Click on a suggestion to select it</li>
        </ul>
      </div>

      <div style={{ marginBottom: 20 }}>
        <h3 style={{ fontSize: 16, marginBottom: 10, fontWeight: 600 }}>Test the Component:</h3>
        <MentionInput
          value={commentText}
          onChange={setCommentText}
          placeholder="Try typing @admin or @user to see autocomplete..."
        />
      </div>

      <div style={{ padding: 16, background: "#f9f9f9", border: "1px solid #ddd", borderRadius: 4 }}>
        <h3 style={{ fontSize: 16, marginBottom: 10, fontWeight: 600 }}>Current Value:</h3>
        <pre style={{ fontSize: 14, whiteSpace: "pre-wrap", wordBreak: "break-word" }}>
          {commentText || "(empty)"}
        </pre>
      </div>

      <div style={{ marginTop: 30, padding: 16, background: "#fff9db", border: "1px solid #fde68a", borderRadius: 4 }}>
        <h3 style={{ fontSize: 16, marginBottom: 10, fontWeight: 600 }}>Test Data:</h3>
        <p style={{ fontSize: 14, lineHeight: 1.6 }}>
          Backend should have demo users from DB-02 seed data:
        </p>
        <ul style={{ fontSize: 14, lineHeight: 1.6, marginLeft: 20 }}>
          <li><code>admin@meepleai.dev</code> - Admin User</li>
          <li><code>editor@meepleai.dev</code> - Editor User</li>
          <li><code>user@meepleai.dev</code> - Regular User</li>
        </ul>
        <p style={{ fontSize: 13, color: "#666", marginTop: 10 }}>
          Try searching: @admin, @editor, @user
        </p>
      </div>
    </div>
  );
}
