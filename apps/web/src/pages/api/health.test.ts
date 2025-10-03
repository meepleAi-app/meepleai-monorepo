import handler from './health';
import { NextApiRequest, NextApiResponse } from 'next';

describe('/api/health', () => {
  it('should return ok: true', () => {
    const req = {} as NextApiRequest;
    const res = {
      status: jest.fn().mockReturnThis(),
      json: jest.fn(),
    } as unknown as NextApiResponse;

    handler(req, res);

    expect(res.status).toHaveBeenCalledWith(200);
    expect(res.json).toHaveBeenCalledWith({ ok: true });
  });
});
