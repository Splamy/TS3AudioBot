# ── Stage 1: build .NET ─────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish TS3AudioBot \
      -c Release \
      -f net10.0 \
      -r linux-x64 \
      --self-contained true \
      -o /app/publish

# ── Stage 2: build WebInterface ─────────────────────────────────────────────
FROM node:18-slim AS webui
WORKDIR /webui
COPY WebInterface/package*.json ./
RUN npm ci
COPY WebInterface/ ./
RUN npm run build

# ── Stage 3: runtime ────────────────────────────────────────────────────────
FROM debian:trixie-slim AS runtime
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      libopus0 \
      ffmpeg \
      libicu-dev \
      yt-dlp \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish /app
COPY --from=webui /webui/dist /app/WebInterface

WORKDIR /data
EXPOSE 58913

ENTRYPOINT ["/app/TS3AudioBot", "--non-interactive", "--config", "/data/ts3audiobot.toml"]
