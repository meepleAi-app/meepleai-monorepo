import { api } from './api';

global.fetch = jest.fn();

describe('api client', () => {
  beforeEach(() => {
    (global.fetch as jest.Mock).mockClear();
  });

  describe('get', () => {
    it('should make a GET request and return JSON data', async () => {
      const mockData = { id: 1, name: 'test' };
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => mockData,
      });

      const result = await api.get('/test');

      expect(global.fetch).toHaveBeenCalledWith('http://localhost:8080/test', {
        method: 'GET',
        credentials: 'include',
      });
      expect(result).toEqual(mockData);
    });

    it('should return null on 401 status', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 401,
      });

      const result = await api.get('/test');

      expect(result).toBeNull();
    });

    it('should throw error on non-ok response (non-401)', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 500,
      });

      await expect(api.get('/test')).rejects.toThrow('API /test 500');
    });
  });

  describe('post', () => {
    it('should make a POST request with body and return JSON data', async () => {
      const mockData = { success: true };
      const requestBody = { key: 'value' };
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => mockData,
      });

      const result = await api.post('/test', requestBody);

      expect(global.fetch).toHaveBeenCalledWith('http://localhost:8080/test', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(requestBody),
      });
      expect(result).toEqual(mockData);
    });

    it('should send empty object when no body provided', async () => {
      const mockData = { success: true };
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => mockData,
      });

      const result = await api.post('/test');

      expect(global.fetch).toHaveBeenCalledWith('http://localhost:8080/test', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({}),
      });
      expect(result).toEqual(mockData);
    });

    it('should throw error on 401 status', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 401,
      });

      await expect(api.post('/test')).rejects.toThrow('Unauthorized');
    });

    it('should throw error on non-ok response (non-401)', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 400,
      });

      await expect(api.post('/test')).rejects.toThrow('API /test 400');
    });
  });

  describe('put', () => {
    it('should make a PUT request with body and return JSON data', async () => {
      const mockData = { updated: true };
      const requestBody = { key: 'updated value' };
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => mockData,
      });

      const result = await api.put('/test', requestBody);

      expect(global.fetch).toHaveBeenCalledWith('http://localhost:8080/test', {
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(requestBody),
      });
      expect(result).toEqual(mockData);
    });

    it('should throw error on 401 status', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 401,
      });

      await expect(api.put('/test', {})).rejects.toThrow('Unauthorized');
    });

    it('should throw error on non-ok response (non-401)', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 404,
      });

      await expect(api.put('/test', {})).rejects.toThrow('API /test 404');
    });
  });
});
