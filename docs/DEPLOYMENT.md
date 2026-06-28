# MeniSpot Deployment Guide

## VPS deploy

This is the recommended production setup for the first real version:

- VPS: Hetzner CX23
- Web server: Caddy
- Backend: .NET API Docker container
- Frontend: Angular static build served by Caddy
- Database: PostgreSQL Docker container
- Uploads: Docker volume mounted to the API container

### 1. Prepare server

```bash
apt update && apt upgrade -y
apt install -y git curl ufw
ufw allow OpenSSH
ufw allow 80
ufw allow 443
ufw enable
curl -fsSL https://get.docker.com | sh
```

### 2. Clone project

```bash
mkdir -p /opt/menispot
cd /opt/menispot
git clone https://github.com/AlijaHodzic/MeniSpot.git .
```

If the repository is private, use a GitHub token or add a deploy key before cloning.

### 3. Create production environment

```bash
cp .env.production.example .env.production
nano .env.production
```

Set strong values for:

```text
POSTGRES_PASSWORD
Jwt__Key
SeedAdmin__Password
AllowedOrigins__0
```

For the first IP-based deploy:

```text
AllowedOrigins__0=http://<server-ip>
```

Production admin password must be stronger than the local development password:

- minimum 8 characters
- at least one number
- at least one uppercase letter
- at least one special character

### 4. Start production containers

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
```

Check status:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f api
```

Open:

```text
http://<server-ip>
http://<server-ip>/health
```

### 5. Later with domain

Point the domain A record to the VPS IPv4 address. Then update `deploy/Caddyfile` from `:80` to the domain name:

```text
your-domain.com {
```

Update `.env.production`:

```text
AllowedOrigins__0=https://your-domain.com
```

Then redeploy:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
```

## Free deploy

Ovaj setup cilja najjeftiniju pocetnu varijantu:

- Database: Neon Free Postgres
- Backend: Render Free Web Service
- Frontend: Cloudflare Pages Free
- Images: trenutno backend upload; kasnije Cloudflare R2

## 1. Neon database

1. Otvori Neon i napravi novi project.
2. Izaberi PostgreSQL database.
3. Kopiraj connection string.
4. U Render env var koristi ADO.NET format ako je ponudjen:

```text
ConnectionStrings__Database=Host=...;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true
```

Ako Neon prikaze URL format, u dashboardu potrazi `.NET` ili `Npgsql` connection string.

## 2. Render backend

Napravi novi Web Service iz GitHub repo-a.

Recommended settings:

```text
Name: menispot-api
Environment: Docker
Dockerfile Path: backend/DigitalMenu.Api/Dockerfile
Health Check Path: /health
```

Environment variables:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Database=<Neon .NET connection string>
Jwt__Issuer=DigitalMenu.Api
Jwt__Audience=DigitalMenu.Web
Jwt__Key=<minimum 32 characters, use a long random value>
Jwt__ExpirationMinutes=60
AllowedOrigins__0=https://<cloudflare-pages-url>
SeedAdmin__Email=<your admin email>
SeedAdmin__Password=<strong temporary admin password>
```

Production admin password must be stronger than local development password:

- minimum 8 characters
- at least one number
- at least one uppercase letter
- at least one special character

Example only:

```text
MeniSpot2026!
```

## 3. Cloudflare Pages frontend

Create a Pages project from GitHub.

Settings:

```text
Framework preset: Angular
Root directory: frontend
Build command: npm run build
Build output directory: dist/frontend/browser
```

When using the VPS setup, the frontend targets:

```text
/api
```

For Render and Cloudflare Pages, update this file to the Render API URL:

```text
frontend/src/app/core/api.config.ts
```

## 4. After deploy

1. Open backend health:

```text
https://menispot-api.onrender.com/health
```

2. Open frontend Pages URL.
3. Login with seeded admin credentials.
4. Create first test restaurant.
5. Open public menu:

```text
https://<cloudflare-pages-url>/menu/<restaurant-slug>
```

## Known free-tier limitation

Render Free can sleep after inactivity. First request can be slow. This is acceptable for demo/testing, but not ideal for real restaurant guests. After first paid client, move backend/database to a small VPS or paid always-on service.
