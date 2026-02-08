import { httpClient } from './http-client';
import type { CreateStrategy, StrategyDto } from './schemas/strategies.schemas';

const BASE_URL = '/api/v1/admin/strategies';

export const strategiesApi = {
  async getAll(): Promise<StrategyDto[]> {
    return httpClient.get<StrategyDto[]>(BASE_URL);
  },

  async getById(id: string): Promise<StrategyDto> {
    return httpClient.get<StrategyDto>(`${BASE_URL}/${id}`);
  },

  async create(data: CreateStrategy): Promise<StrategyDto> {
    return httpClient.post<StrategyDto>(BASE_URL, data);
  },

  async update(id: string, data: CreateStrategy): Promise<StrategyDto> {
    return httpClient.put<StrategyDto>(`${BASE_URL}/${id}`, data);
  },

  async delete(id: string): Promise<void> {
    return httpClient.delete<void>(`${BASE_URL}/${id}`);
  },
};
