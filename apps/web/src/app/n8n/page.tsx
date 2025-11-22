'use client';

import { useEffect, useState } from "react";
import Link from "next/link";
import { cn } from "@/lib/utils";

type N8nConfig = {
  id: string;
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

export const apiBase = process.env.NEXT_PUBLIC_API_BASE || "http://localhost:5080";

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
      const res = await fetch(`${apiBase}/admin/n8n`, {
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
        ? `${apiBase}/admin/n8n/${editingConfig.id}`
        : `${apiBase}/admin/n8n`;

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
      const res = await fetch(`${apiBase}/admin/n8n/${configId}`, {
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
      const res = await fetch(`${apiBase}/admin/n8n/${configId}/test`, {
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
      const res = await fetch(`${apiBase}/admin/n8n/${config.id}`, {
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
      <main className="p-6 font-sans max-w-7xl mx-auto">
        <h1>Loading...</h1>
      </main>
    );
  }

  if (error) {
    return (
      <main className="p-6 font-sans max-w-7xl mx-auto">
        <h1>Error</h1>
        <p className="text-red-600">{error}</p>
        <Link href="/" className="text-blue-600 hover:underline">Back to Home</Link>
      </main>
    );
  }

  return (
    <main className="p-6 font-sans max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="m-0">n8n Workflow Management</h1>
          <p className="mt-2 mb-0 text-slate-500">
            Configure n8n endpoints and credentials for workflow automation
          </p>
        </div>
        <div className="flex gap-3">
          <button
            onClick={() => {
              setEditingConfig(null);
              setFormData({ name: "", baseUrl: "", apiKey: "", webhookUrl: "" });
              setShowForm(!showForm);
            }}
            className={cn(
              "px-4 py-2 text-white border-none rounded cursor-pointer",
              showForm ? "bg-slate-500" : "bg-green-600 hover:bg-green-700"
            )}
          >
            {showForm ? "Cancel" : "Add Configuration"}
          </button>
          <Link
            href="/"
            className="px-4 py-2 bg-blue-600 text-white no-underline rounded inline-block hover:bg-blue-700"
          >
            Back to Home
          </Link>
        </div>
      </div>

      {showForm && (
        <div className="p-6 border border-gray-300 rounded-lg bg-white mb-6">
          <h3 className="mt-0">{editingConfig ? "Edit Configuration" : "New Configuration"}</h3>
          <form onSubmit={handleSubmit}>
            <div className="mb-4">
              <label className="block mb-1 text-sm font-semibold">
                Name *
              </label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                required
                className="w-full p-3 text-sm border border-gray-300 rounded"
                placeholder="Production n8n"
              />
            </div>

            <div className="mb-4">
              <label className="block mb-1 text-sm font-semibold">
                Base URL *
              </label>
              <input
                type="url"
                value={formData.baseUrl}
                onChange={(e) => setFormData({ ...formData, baseUrl: e.target.value })}
                required
                className="w-full p-3 text-sm border border-gray-300 rounded"
                placeholder="http://localhost:5678"
              />
            </div>

            <div className="mb-4">
              <label className="block mb-1 text-sm font-semibold">
                API Key {editingConfig ? "(leave empty to keep current)" : "*"}
              </label>
              <input
                type="password"
                value={formData.apiKey}
                onChange={(e) => setFormData({ ...formData, apiKey: e.target.value })}
                required={!editingConfig}
                className="w-full p-3 text-sm border border-gray-300 rounded"
                placeholder="n8n API key"
              />
            </div>

            <div className="mb-4">
              <label className="block mb-1 text-sm font-semibold">
                Webhook URL (optional)
              </label>
              <input
                type="url"
                value={formData.webhookUrl}
                onChange={(e) => setFormData({ ...formData, webhookUrl: e.target.value })}
                className="w-full p-3 text-sm border border-gray-300 rounded"
                placeholder="http://localhost:5678/webhook"
              />
            </div>

            <button
              type="submit"
              className="px-4 py-2 bg-blue-600 text-white border-none rounded cursor-pointer text-sm font-semibold hover:bg-blue-700"
            >
              {editingConfig ? "Update Configuration" : "Create Configuration"}
            </button>
          </form>
        </div>
      )}

      {configs.length === 0 ? (
        <div className="p-12 text-center text-slate-500 border border-gray-300 rounded-lg">
          <p>No n8n configurations found. Click &quot;Add Configuration&quot; to create one.</p>
        </div>
      ) : (
        <div className="grid gap-4">
          {configs.map((config) => (
            <div
              key={config.id}
              className="p-6 border border-gray-300 rounded-lg bg-white"
            >
              <div className="flex justify-between items-start mb-4">
                <div>
                  <div className="flex items-center gap-3 mb-2">
                    <h3 className="m-0">{config.name}</h3>
                    <span
                      className={cn(
                        "px-2 py-1 text-xs font-semibold rounded",
                        config.isActive
                          ? "bg-green-100 text-green-600"
                          : "bg-gray-100 text-slate-500"
                      )}
                    >
                      {config.isActive ? "Active" : "Inactive"}
                    </span>
                  </div>
                  <p className="mt-1 mb-0 text-slate-500 text-sm">
                    {config.baseUrl}
                  </p>
                  {config.webhookUrl && (
                    <p className="mt-1 mb-0 text-slate-500 text-sm">
                      Webhook: {config.webhookUrl}
                    </p>
                  )}
                </div>
                <div className="flex gap-2">
                  <button
                    onClick={() => handleTest(config.id)}
                    disabled={testing === config.id}
                    className={cn(
                      "px-3 py-1.5 bg-yellow-500 text-white border-none rounded text-[13px]",
                      testing === config.id
                        ? "cursor-not-allowed opacity-60"
                        : "cursor-pointer hover:bg-yellow-600"
                    )}
                  >
                    {testing === config.id ? "Testing..." : "Test"}
                  </button>
                  <button
                    onClick={() => handleToggleActive(config)}
                    className={cn(
                      "px-3 py-1.5 text-white border-none rounded cursor-pointer text-[13px]",
                      config.isActive
                        ? "bg-slate-500 hover:bg-slate-600"
                        : "bg-green-600 hover:bg-green-700"
                    )}
                  >
                    {config.isActive ? "Deactivate" : "Activate"}
                  </button>
                  <button
                    onClick={() => handleEdit(config)}
                    className="px-3 py-1.5 bg-blue-600 text-white border-none rounded cursor-pointer text-[13px] hover:bg-blue-700"
                  >
                    Edit
                  </button>
                  <button
                    onClick={() => handleDelete(config.id)}
                    className="px-3 py-1.5 bg-red-600 text-white border-none rounded cursor-pointer text-[13px] hover:bg-red-700"
                  >
                    Delete
                  </button>
                </div>
              </div>

              {config.lastTestedAt && (
                <div className="p-3 bg-gray-50 rounded">
                  <div className="text-xs text-slate-500 mb-1">
                    Last tested: {new Date(config.lastTestedAt).toLocaleString()}
                  </div>
                  <div
                    className={cn(
                      "text-[13px] font-semibold",
                      config.lastTestResult?.includes("successful")
                        ? "text-green-600"
                        : "text-red-600"
                    )}
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
