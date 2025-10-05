import type { NextApiRequest, NextApiResponse } from "next";
import handler from "../health";

describe("/api/health", () => {
  it("returns a 200 ok payload", () => {
    const req = {} as NextApiRequest;
    const res: Partial<NextApiResponse> = {};
    res.json = jest.fn();
    res.status = jest.fn(() => res as NextApiResponse);

    handler(req, res as NextApiResponse);

    expect(res.status).toHaveBeenCalledWith(200);
    expect(res.json).toHaveBeenCalledWith({ ok: true });
  });
});
