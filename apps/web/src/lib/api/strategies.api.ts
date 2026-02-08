import { HttpClient } from './core/httpClient';
import type { CreateStrategy, StrategyDto } from './schemas/strategies.schemas';

const httpClient = new HttpClient({});
const BASE_URL = '/api/v1/admin/strategies';

export const strategiesApi = {
  async getAll(): Promise<StrategyDto[]> {
    const result = await httpClient.get<StrategyDto[]>(BASE_URL);
    return result ?? [];
  },

  async getById(id: string): Promise<StrategyDto> {
    const result = await httpClient.get<StrategyDto>(`${BASE_URL}/${id}`);
    if (!result) throw new Error(`Strategy ${id} not found`);
    return result;
  },

  async create(data: CreateStrategy): Promise<StrategyDto> {
    const result = await httpClient.post<StrategyDto>(BASE_URL, data);
    if (!result) throw new Error('Failed to create strategy');
    return result;
  },

  async update(id: string, data: CreateStrategy): Promise<StrategyDto> {
    const result = await httpClient.put<StrategyDto>(`${BASE_URL}/${id}`, data);
    if (!result) throw new Error(`Failed to update strategy ${id}`);
    return result;
  },

  async delete(id: string): Promise<void> {
    await httpClient.delete(`${BASE_URL}/${id}`);
  },
};