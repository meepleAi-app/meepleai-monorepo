import { useEffect, useState } from "react";
import Link from "next/link";

type N8nConfig = {
  id: string;
  tenantId: string;
  name: string;
  baseUrl: string;
  webhookUrl: string | null;
  isActive: boolean;
  lastTestedAt: string | null;
  lastTestResult: string | null;
  createdAt: string;
  updatedAt: string;
};

type TestResult = {
  success: boolean;
  message: string;
  latencyMs: number | null;
};

export default function N8nWorkflowManagement() {
  const [configs, setConfigs] = useState<N8nConfig[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [editingConfig, setEditingConfig] = useState<N8nConfig | null>(null);
  const [testing, setTesting] = useState<string | null>(null);

  const [formData, setFormData] = useState({
    name: "",
    baseUrl: "",
    apiKey: "",
    webhookUrl: "",
  });

  useEffect(() => {
    fetchConfigs();
  }, []);

  const fetchConfigs = async () => {
    try {
      setLoading(true);
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_BASE}/admin/n8n`, {
        credentials: "include"
      });

      if (!res.ok) {
        throw new Error("Failed to fetch n8n configurations");
      }

      const data = await res.json();
      setConfigs(data.configs);
      setLoading(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "An error occurred");
      setLoading(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    try {
      const url = editingConfig
        ? `${process.env.NEXT_PUBLIC_API_BASE}/admin/n8n/${editingConfig.id}`
        : `${process.env.NEXT_PUBLIC_API_BASE}/admin/n8n`;

      const method = editingConfig ? "PUT" : "POST";

      const payload = editingConfig
        ? {
            name: formData.name !== editingConfig.name ? formData.name : undefined,
            baseUrl: formData.baseUrl !== editingConfig.baseUrl ? formData.baseUrl : undefined,
            apiKey: formData.apiKey ? formData.apiKey : undefined,
            webhookUrl: formData.webhookUrl !== editingConfig.webhookUrl ? formData.webhookUrl || null : undefined,
          }
        : {
            name: formData.name,
            baseUrl: formData.baseUrl,
            apiKey: formData.apiKey,
            webhookUrl: formData.webhookUrl || null,
          };

      const res = await fetch(url, {
        method,
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify(payload),
      });

      if (!res.ok) {
        const errData = await res.json();
        throw new Error(errData.error || "Failed to save configuration");
      }

      setShowForm(false);
      setEditingConfig(null);
      setFormData({ name: "", baseUrl: "", apiKey: "", webhookUrl: "" });
      fetchConfigs();
    } catch (err) {
      alert(err instanceof Error ? err.message : "Failed to save configuration");
    }
  };

  const handleEdit = (config: N8nConfig) => {
    setEditingConfig(config);
    setFormData({
      name: config.name,
      baseUrl: config.baseUrl,
      apiKey: "",
      webhookUrl: config.webhookUrl || "",
    });
    setShowForm(true);
  };

  const handleDelete = async (configId: string) => {
    if (!confirm("Are you sure you want to delete this configuration?")) {
      return;
    }

    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_BASE}/admin/n8n/${configId}`, {
        method: "DELETE",
        credentials: "include",
      });

      if (!res.ok) {
        throw new Error("Failed to delete configuration");
      }

      fetchConfigs();
    } catch (err) {
      alert(err instanceof Error ? err.message : "Failed to delete configuration");
    }
  };

  const handleTest = async (configId: string) => {
    setTesting(configId);
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_BASE}/admin/n8n/${configId}/test`, {
        method: "POST",
        credentials: "include",
      });

      if (!res.ok) {
        throw new Error("Failed to test configuration");
      }

      const result: TestResult = await res.json();
      alert(result.message);
      fetchConfigs();
    } catch (err) {
      alert(err instanceof Error ? err.message : "Failed to test configuration");
    } finally {
      setTesting(null);
    }
  };

  const handleToggleActive = async (config: N8nConfig) => {
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_BASE}/admin/n8n/${config.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ isActive: !config.isActive }),
      });

      if (!res.ok) {
        throw new Error("Failed to update configuration");
      }

      fetchConfigs();
    } catch (err) {
      alert(err instanceof Error ? err.message : "Failed to update configuration");
    }
  };

  if (loading) {
    return (
      <main style={{ padding: 24, fontFamily: "sans-serif", maxWidth: 1400, margin: "0 auto" }}>
        <h1>Loading...</h1>
      </main>
    );
  }

  if (error) {
    return (
      <main style={{ padding: 24, fontFamily: "sans-serif", maxWidth: 1400, margin: "0 auto" }}>
        <h1>Error</h1>
        <p style={{ color: "#d93025" }}>{error}</p>
        <Link href="/" style={{ color: "#1a73e8" }}>Back to Home</Link>
      </main>
    );
  }

  return (
    <main style={{ padding: 24, fontFamily: "sans-serif", maxWidth: 1400, margin: "0 auto" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
        <div>
          <h1 style={{ margin: 0 }}>n8n Workflow Management</h1>
          <p style={{ margin: "8px 0 0 0", color: "#5f6368" }}>
            Configure n8n endpoints and credentials for workflow automation
          </p>
        </div>
        <div style={{ display: "flex", gap: 12 }}>
          <button
            onClick={() => {
              setEditingConfig(null);
              setFormData({ name: "", baseUrl: "", apiKey: "", webhookUrl: "" });
              setShowForm(!showForm);
            }}
            style={{
              padding: "8px 16px",
              background: showForm ? "#5f6368" : "#0f9d58",
              color: "white",
              border: "none",
              borderRadius: 4,
              cursor: "pointer"
            }}
          >
            {showForm ? "Cancel" : "Add Configuration"}
          </button>
          <Link
            href="/"
            style={{
              padding: "8px 16px",
              background: "#1a73e8",
              color: "white",
              textDecoration: "none",
              borderRadius: 4,
              display: "inline-block"
            }}
          >
            Back to Home
          </Link>
        </div>
      </div>

      {showForm && (
        <div style={{ padding: 24, border: "1px solid #dadce0", borderRadius: 8, background: "white", marginBottom: 24 }}>
          <h3 style={{ marginTop: 0 }}>{editingConfig ? "Edit Configuration" : "New Configuration"}</h3>
          <form onSubmit={handleSubmit}>
            <div style={{ marginBottom: 16 }}>
              <label style={{ display: "block", marginBottom: 4, fontSize: 14, fontWeight: 600 }}>
                Name *
              </label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                required
                style={{
                  width: "100%",
                  padding: 12,
                  fontSize: 14,
                  border: "1px solid #dadce0",
                  borderRadius: 4
                }}
                placeholder="Production n8n"
              />
            </div>

            <div style={{ marginBottom: 16 }}>
              <label style={{ display: "block", marginBottom: 4, fontSize: 14, fontWeight: 600 }}>
                Base URL *
              </label>
              <input
                type="url"
                value={formData.baseUrl}
                onChange={(e) => setFormData({ ...formData, baseUrl: e.target.value })}
                required
                style={{
                  width: "100%",
                  padding: 12,
                  fontSize: 14,
                  border: "1px solid #dadce0",
                  borderRadius: 4
                }}
                placeholder="http://localhost:5678"
              />
            </div>

            <div style={{ marginBottom: 16 }}>
              <label style={{ display: "block", marginBottom: 4, fontSize: 14, fontWeight: 600 }}>
                API Key {editingConfig ? "(leave empty to keep current)" : "*"}
              </label>
              <input
                type="password"
                value={formData.apiKey}
                onChange={(e) => setFormData({ ...formData, apiKey: e.target.value })}
                required={!editingConfig}
                style={{
                  width: "100%",
                  padding: 12,
                  fontSize: 14,
                  border: "1px solid #dadce0",
                  borderRadius: 4
                }}
                placeholder="n8n API key"
              />
            </div>

            <div style={{ marginBottom: 16 }}>
              <label style={{ display: "block", marginBottom: 4, fontSize: 14, fontWeight: 600 }}>
                Webhook URL (optional)
              </label>
              <input
                type="url"
                value={formData.webhookUrl}
                onChange={(e) => setFormData({ ...formData, webhookUrl: e.target.value })}
                style={{
                  width: "100%",
                  padding: 12,
                  fontSize: 14,
                  border: "1px solid #dadce0",
                  borderRadius: 4
                }}
                placeholder="http://localhost:5678/webhook"
              />
            </div>

            <button
              type="submit"
              style={{
                padding: "8px 16px",
                background: "#1a73e8",
                color: "white",
                border: "none",
                borderRadius: 4,
                cursor: "pointer",
                fontSize: 14,
                fontWeight: 600
              }}
            >
              {editingConfig ? "Update Configuration" : "Create Configuration"}
            </button>
          </form>
        </div>
      )}

      {configs.length === 0 ? (
        <div style={{ padding: 48, textAlign: "center", color: "#5f6368", border: "1px solid #dadce0", borderRadius: 8 }}>
          <p>No n8n configurations found. Click &quot;Add Configuration&quot; to create one.</p>
        </div>
      ) : (
        <div style={{ display: "grid", gap: 16 }}>
          {configs.map((config) => (
            <div
              key={config.id}
              style={{
                padding: 24,
                border: "1px solid #dadce0",
                borderRadius: 8,
                background: "white"
              }}
            >
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "start", marginBottom: 16 }}>
                <div>
                  <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 8 }}>
                    <h3 style={{ margin: 0 }}>{config.name}</h3>
                    <span
                      style={{
                        padding: "4px 8px",
                        background: config.isActive ? "#e8f5e9" : "#f5f5f5",
                        color: config.isActive ? "#0f9d58" : "#5f6368",
                        fontSize: 12,
                        fontWeight: 600,
                        borderRadius: 4
                      }}
                    >
                      {config.isActive ? "Active" : "Inactive"}
                    </span>
                  </div>
                  <p style={{ margin: "4px 0 0 0", color: "#5f6368", fontSize: 14 }}>
                    {config.baseUrl}
                  </p>
                  {config.webhookUrl && (
                    <p style={{ margin: "4px 0 0 0", color: "#5f6368", fontSize: 14 }}>
                      Webhook: {config.webhookUrl}
                    </p>
                  )}
                </div>
                <div style={{ display: "flex", gap: 8 }}>
                  <button
                    onClick={() => handleTest(config.id)}
                    disabled={testing === config.id}
                    style={{
                      padding: "6px 12px",
                      background: "#f9ab00",
                      color: "white",
                      border: "none",
                      borderRadius: 4,
                      cursor: testing === config.id ? "not-allowed" : "pointer",
                      fontSize: 13,
                      opacity: testing === config.id ? 0.6 : 1
                    }}
                  >
                    {testing === config.id ? "Testing..." : "Test"}
                  </button>
                  <button
                    onClick={() => handleToggleActive(config)}
                    style={{
                      padding: "6px 12px",
                      background: config.isActive ? "#5f6368" : "#0f9d58",
                      color: "white",
                      border: "none",
                      borderRadius: 4,
                      cursor: "pointer",
                      fontSize: 13
                    }}
                  >
                    {config.isActive ? "Deactivate" : "Activate"}
                  </button>
                  <button
                    onClick={() => handleEdit(config)}
                    style={{
                      padding: "6px 12px",
                      background: "#1a73e8",
                      color: "white",
                      border: "none",
                      borderRadius: 4,
                      cursor: "pointer",
                      fontSize: 13
                    }}
                  >
                    Edit
                  </button>
                  <button
                    onClick={() => handleDelete(config.id)}
                    style={{
                      padding: "6px 12px",
                      background: "#d93025",
                      color: "white",
                      border: "none",
                      borderRadius: 4,
                      cursor: "pointer",
                      fontSize: 13
                    }}
                  >
                    Delete
                  </button>
                </div>
              </div>

              {config.lastTestedAt && (
                <div style={{ padding: 12, background: "#f8f9fa", borderRadius: 4 }}>
                  <div style={{ fontSize: 12, color: "#5f6368", marginBottom: 4 }}>
                    Last tested: {new Date(config.lastTestedAt).toLocaleString()}
                  </div>
                  <div
                    style={{
                      fontSize: 13,
                      color: config.lastTestResult?.includes("successful") ? "#0f9d58" : "#d93025",
                      fontWeight: 600
                    }}
                  >
                    {config.lastTestResult}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </main>
  );
}
