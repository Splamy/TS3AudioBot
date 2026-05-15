# ── Stage 1: build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish TS3AudioBot \
      -c Release \
      -f net10.0 \
      -r linux-x64 \
      --self-contained true \
      -o /app/publish

# ── Stage 2: runtime ────────────────────────────────────────────────────────
FROM debian:trixie-slim AS runtime
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      libopus0 \
      ffmpeg \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish /app

WORKDIR /data
EXPOSE 58913

ENTRYPOINT ["/app/TS3AudioBot", "--non-interactive", "--config", "/data/ts3audiobot.toml"]
