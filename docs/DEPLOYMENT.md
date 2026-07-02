# MeniSpot Deployment Guide

This document describes the current production deployment model for MeniSpot.

## Production Overview

Recommended production stack:

- DNS and edge proxy: Cloudflare
- VPS reverse proxy: Caddy
- Frontend: Angular static build served by the web container
- Backend: ASP.NET Core API container
- Database: PostgreSQL container on the private Docker network
- Uploads: Docker volume mounted to the API container
- Runtime: Docker Compose

High-level request flow:

```text
Visitor
  |
  v
Cloudflare
  |
  v
Caddy reverse proxy
  |
  +--> Angular frontend
  |
  +--> ASP.NET Core API
          |
          v
      PostgreSQL
```

PostgreSQL should not be exposed to the public internet.

## 1. Prepare Server

Install Docker and allow only the required public ports:

```bash
apt update && apt upgrade -y
apt install -y git curl ufw
ufw allow OpenSSH
ufw allow 80
ufw allow 443
ufw enable
curl -fsSL https://get.docker.com | sh
```

## 2. Clone Project

```bash
mkdir -p /opt/menispot
cd /opt/menispot
git clone https://github.com/AlijaHodzic/MeniSpot.git .
```

If the repository is private, use a deploy key or another restricted GitHub access method.

## 3. Configure Environment

Create the production environment file:

```bash
cp .env.production.example .env.production
nano .env.production
```

Set strong production values for:

```text
POSTGRES_PASSWORD
Jwt__Key
SeedAdmin__Password
AllowedOrigins__0
AllowedOrigins__1
LeadNotifications__FormspreeEndpoint
```

Example allowed origins for the live domain:

```text
AllowedOrigins__0=https://menispot.com
AllowedOrigins__1=https://www.menispot.com
LeadNotifications__FormspreeEndpoint=https://formspree.io/f/<your-form-id>
```

The public lead form is submitted through the API, which then forwards valid leads to Formspree. In Formspree, also enable domain restriction for `menispot.com` and `www.menispot.com`, and mark spam submissions as spam so the Formspree filter learns from them.

Protect the environment file:

```bash
chmod 600 /opt/menispot/.env.production
```

Never commit `.env.production`.

## 4. Configure Cloudflare

Create DNS records for the domain:

```text
Type: A
Name: @
Content: <server-ip>
Proxy: enabled

Type: A
Name: www
Content: <server-ip>
Proxy: enabled
```

Set SSL/TLS mode to:

```text
Full (strict)
```

## 5. Configure Caddy

The production Caddy configuration is stored in:

```text
deploy/Caddyfile
```

It should route the root domain and `www` traffic to the application and redirect `www` to the canonical domain when configured that way.

## 6. Start Production Containers

```bash
cd /opt/menispot
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
```

Check container status:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml ps
```

Check logs:

```bash
docker compose -f docker-compose.prod.yml logs -f web
docker compose -f docker-compose.prod.yml logs -f api
```

## 7. Verify Production

Open:

```text
https://menispot.com
https://menispot.com/health
```

Also verify:

- public menu pages load over HTTPS
- admin login works
- owner dashboard opens
- image uploads render through `/uploads/...`
- QR menu links use the public domain

## Updating Production

Pull the latest code and rebuild the changed containers:

```bash
cd /opt/menispot
git pull
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build
```

If the repository is private, configure a restricted deploy key on the server before running `git pull`.

If only frontend/Caddy files changed, rebuilding the web container is enough:

```bash
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build web
```

## Rollback

Use Git to return to a known working commit, then rebuild:

```bash
cd /opt/menispot
git log --oneline -5
git checkout <commit-sha>
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build
```

After confirming the application is stable, move the production branch pointer intentionally instead of leaving the server detached long-term.

## Operational Notes

- Keep PostgreSQL inside the Docker network.
- Do not publish port `5432`.
- Keep `.env.production` readable only by root or the deploy user.
- Store backups outside the Git repository.
- Rotate secrets immediately if they are exposed.
- Keep Docker images and server packages updated.

Backups are documented in [BACKUPS.md](BACKUPS.md).
Security notes are documented in [SECURITY.md](SECURITY.md).
