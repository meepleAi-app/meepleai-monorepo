/**
 * Docker Container Management Schemas
 * Issue #139: Container management API types
 */

import { z } from 'zod';

export const ContainerInfoSchema = z.object({
  id: z.string(),
  name: z.string(),
  image: z.string(),
  state: z.string(),
  status: z.string(),
  created: z.string(),
  labels: z.record(z.string(), z.string()),
});

export const ContainerLogsSchema = z.object({
  containerId: z.string(),
  containerName: z.string(),
  lines: z.array(z.string()),
  fetchedAt: z.string(),
});

export type ContainerInfo = z.infer<typeof ContainerInfoSchema>;
export type ContainerLogs = z.infer<typeof ContainerLogsSchema>;
