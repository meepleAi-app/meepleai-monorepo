import { apiClient } from "./client";

export interface AIInsight {
  id: string;
  type: "backlog" | "rulesReminder" | "recommendation" | "streak";
  icon: string;
  title: string;
  description: string;
  actionUrl: string;
  actionLabel: string;
  priority: number;
}

export interface DashboardInsightsResponse {
  insights: AIInsight[];
  generatedAt: string;
  nextRefresh: string;
}

export async function getAIInsights(): Promise<DashboardInsightsResponse> {
  const response = await apiClient.get<DashboardInsightsResponse>("/dashboard/insights");
  return response.data;
}
