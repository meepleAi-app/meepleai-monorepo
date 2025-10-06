import type { NextApiRequest, NextApiResponse } from "next";
import handler from "../health";

describe("health API handler", () => {
  it("responds with status 200 and ok true", () => {
    const status = jest.fn().mockReturnThis();
    const json = jest.fn();
    const res = {
      status,
      json,
    } as unknown as NextApiResponse;

    handler({} as NextApiRequest, res);

    expect(status).toHaveBeenCalledTimes(1);
    expect(status).toHaveBeenCalledWith(200);
    expect(json).toHaveBeenCalledTimes(1);
    expect(json).toHaveBeenCalledWith({ ok: true });
  });
});
