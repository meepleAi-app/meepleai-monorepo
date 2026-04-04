"""Minimal HTTP API for SSH tunnel management with bearer auth."""

import os
import logging
from functools import wraps

from flask import Flask, request, jsonify

from tunnel_manager import TunnelManager

app = Flask(__name__)
logging.basicConfig(level=logging.INFO)

manager = TunnelManager()

AUTH_TOKEN = os.environ.get("SIDECAR_AUTH_TOKEN")


def require_auth(f):
    @wraps(f)
    def decorated(*args, **kwargs):
        if not AUTH_TOKEN:
            return jsonify({"error": "SIDECAR_AUTH_TOKEN not configured"}), 500
        auth = request.headers.get("Authorization", "")
        if auth != f"Bearer {AUTH_TOKEN}":
            return jsonify({"error": "Unauthorized"}), 401
        return f(*args, **kwargs)
    return decorated


@app.route("/health", methods=["GET"])
def health():
    return "OK", 200


@app.route("/status", methods=["GET"])
@require_auth
def status():
    return jsonify({
        "status": manager.status,
        "uptime_seconds": manager.uptime_seconds,
        "message": manager.message
    })


@app.route("/open", methods=["POST"])
@require_auth
def open_tunnel():
    data = request.get_json(silent=True) or {}
    host = data.get("host", os.environ.get("DEFAULT_HOST", "204.168.135.69"))
    port = int(data.get("port", 5432))
    user = data.get("user", "deploy")
    local_port = int(data.get("localPort", 15432))

    result = manager.open(host, port, user, local_port)
    status_code = 200 if result["status"] in ("open", "opening") else 400
    return jsonify(result), status_code


@app.route("/close", methods=["DELETE"])
@require_auth
def close_tunnel():
    result = manager.close()
    return jsonify(result)


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=2222)
