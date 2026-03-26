FROM python:3.12-alpine3.19

RUN apk add --no-cache openssh-client

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY server.py tunnel_manager.py ./

RUN mkdir -p /root/.ssh && chmod 700 /root/.ssh

EXPOSE 2222

HEALTHCHECK --interval=10s --timeout=3s --retries=3 \
  CMD wget -q --spider http://localhost:2222/health || exit 1

CMD ["python", "server.py"]
