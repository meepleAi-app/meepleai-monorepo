"""SSH tunnel process manager with auto-close and host whitelist."""

import os
import subprocess
import threading
import time
import logging

logger = logging.getLogger(__name__)

ALLOWED_HOSTS = os.environ.get("ALLOWED_HOSTS", "").split(",")
MAX_DURATION = int(os.environ.get("MAX_TUNNEL_DURATION", "3600"))
SSH_KEY_PATH = os.environ.get("SSH_KEY_PATH", "/root/.ssh/key")


class TunnelManager:
    def __init__(self):
        self._process = None
        self._status = "closed"
        self._message = None
        self._started_at = None
        self._lock = threading.Lock()
        self._timer = None

    @property
    def status(self):
        with self._lock:
            if self._process and self._process.poll() is not None:
                self._status = "error"
                self._message = f"SSH process exited with code {self._process.returncode}"
                self._process = None
            return self._status

    @property
    def uptime_seconds(self):
        if self._started_at and self._status == "open":
            return int(time.time() - self._started_at)
        return 0

    @property
    def message(self):
        return self._message

    def open(self, host, port, user, local_port):
        with self._lock:
            if self._process and self._process.poll() is None:
                return {"status": "open", "message": "Tunnel already open"}

            host = host.strip()
            if host not in ALLOWED_HOSTS:
                self._status = "error"
                self._message = f"Host {host} not in allowed list"
                return {"status": "error", "message": self._message}

            if not os.path.isfile(SSH_KEY_PATH):
                self._status = "error"
                self._message = f"SSH key not found at {SSH_KEY_PATH}"
                return {"status": "error", "message": self._message}

            self._status = "opening"
            self._message = f"Connecting to {host}..."

            cmd = [
                "ssh", "-N", "-o", "StrictHostKeyChecking=accept-new",
                "-o", "ServerAliveInterval=30",
                "-o", "ServerAliveCountMax=3",
                "-o", "ConnectTimeout=10",
                "-i", SSH_KEY_PATH,
                "-L", f"0.0.0.0:{local_port}:localhost:{port}",
                f"{user}@{host}"
            ]

            try:
                self._process = subprocess.Popen(
                    cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE
                )
                time.sleep(2)
                if self._process.poll() is not None:
                    stderr = self._process.stderr.read().decode() if self._process.stderr else ""
                    self._status = "error"
                    self._message = f"SSH failed: {stderr.strip()}"
                    self._process = None
                    return {"status": "error", "message": self._message}

                self._status = "open"
                self._started_at = time.time()
                self._message = f"Tunnel open: localhost:{local_port} -> {host}:{port}"

                if self._timer:
                    self._timer.cancel()
                self._timer = threading.Timer(MAX_DURATION, self._auto_close)
                self._timer.daemon = True
                self._timer.start()

                return {"status": "open", "message": self._message}

            except Exception as e:
                self._status = "error"
                self._message = str(e)
                return {"status": "error", "message": self._message}

    def close(self):
        with self._lock:
            if self._timer:
                self._timer.cancel()
                self._timer = None
            if self._process and self._process.poll() is None:
                self._process.terminate()
                self._process.wait(timeout=5)
            self._process = None
            self._status = "closed"
            self._started_at = None
            self._message = None
            return {"status": "closed"}

    def _auto_close(self):
        logger.warning("Auto-closing tunnel after max duration")
        self.close()
