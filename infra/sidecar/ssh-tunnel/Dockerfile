FROM python:3.12-alpine3.19

RUN apk add --no-cache openssh-client

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY server.py tunnel_manager.py ./

# Create non-root user for security (consistent with all other services)
RUN addgroup -g 1001 -S tunnel && \
    adduser -S tunnel -u 1001 -G tunnel && \
    mkdir -p /home/tunnel/.ssh && chmod 700 /home/tunnel/.ssh && \
    chown -R tunnel:tunnel /app /home/tunnel/.ssh

# Override default SSH_KEY_PATH to match non-root user home
ENV SSH_KEY_PATH=/home/tunnel/.ssh/key

USER tunnel

EXPOSE 2222

HEALTHCHECK --interval=10s --timeout=3s --retries=3 \
  CMD wget -q --spider http://localhost:2222/health || exit 1

CMD ["python", "server.py"]
