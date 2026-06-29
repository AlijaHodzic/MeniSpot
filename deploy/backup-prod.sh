#!/usr/bin/env bash
set -euo pipefail
umask 077

PROJECT_DIR="${PROJECT_DIR:-/opt/menispot}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-.env.production}"
BACKUP_ROOT="${BACKUP_ROOT:-$PROJECT_DIR/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"
BACKUP_REMOTE="${BACKUP_REMOTE:-}"
BACKUP_ENCRYPTION_PASSWORD="${BACKUP_ENCRYPTION_PASSWORD:-}"

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1"
    exit 1
  fi
}

require_command docker
require_command tar
require_command find

cd "$PROJECT_DIR"

if [ ! -f "$ENV_FILE" ]; then
  echo "Missing $ENV_FILE in $PROJECT_DIR"
  exit 1
fi

project_real="$(pwd -P)"
mkdir -p "$BACKUP_ROOT"
backup_root_real="$(cd "$BACKUP_ROOT" && pwd -P)"

case "$backup_root_real" in
  "$project_real/.git"|"$project_real/.git"/*|"$project_real/docs"|"$project_real/docs"/*|"$project_real/frontend"|"$project_real/frontend"/*|"$project_real/backend"|"$project_real/backend"/*|"$project_real/deploy"|"$project_real/deploy"/*)
    echo "Refusing to write backups inside a source-controlled or public project folder: $backup_root_real"
    exit 1
    ;;
esac

if [ "$backup_root_real" = "$project_real" ]; then
  echo "Refusing to write backup archives directly into the project root."
  exit 1
fi

chmod 700 "$backup_root_real"

set -a
source "$ENV_FILE"
set +a

if [ -z "${POSTGRES_USER:-}" ] || [ -z "${POSTGRES_DB:-}" ]; then
  echo "POSTGRES_USER and POSTGRES_DB must be set in $ENV_FILE"
  exit 1
fi

timestamp="$(date +%Y%m%d-%H%M%S)"
work_dir="$backup_root_real/.work-$timestamp"
archive_name="menispot-backup-$timestamp.tar.gz"
archive_path="$backup_root_real/$archive_name"
upload_path="$archive_path"

mkdir -p "$work_dir"
chmod 700 "$work_dir"

cleanup() {
  rm -rf "$work_dir"
}
trap cleanup EXIT

echo "Creating PostgreSQL dump..."
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T postgres \
  pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists \
  > "$work_dir/database.sql"

echo "Archiving uploaded images..."
if docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" ps --services --filter status=running | grep -qx "api"; then
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T api \
    tar -czf - -C /app/wwwroot/uploads . \
    > "$work_dir/uploads.tar.gz"
else
  echo "API container is not running, creating empty uploads archive."
  tar -czf "$work_dir/uploads.tar.gz" --files-from /dev/null
fi

cat > "$work_dir/manifest.txt" <<EOF
MeniSpot production backup
Created: $(date -Iseconds)
Project: $PROJECT_DIR
Database: $POSTGRES_DB
Includes:
- database.sql
- uploads.tar.gz
Does not include:
- .env.production
- SSH keys
- Docker images
EOF

echo "Packing backup archive..."
tar -czf "$archive_path" -C "$work_dir" database.sql uploads.tar.gz manifest.txt
chmod 600 "$archive_path"

if [ -n "$BACKUP_ENCRYPTION_PASSWORD" ]; then
  if ! command -v openssl >/dev/null 2>&1; then
    echo "BACKUP_ENCRYPTION_PASSWORD is set, but openssl is not installed."
    exit 1
  fi

  encrypted_path="$archive_path.enc"
  echo "Encrypting backup archive..."
  openssl enc -aes-256-cbc -salt -pbkdf2 -iter 200000 \
    -in "$archive_path" \
    -out "$encrypted_path" \
    -pass env:BACKUP_ENCRYPTION_PASSWORD
  chmod 600 "$encrypted_path"

  if command -v shred >/dev/null 2>&1; then
    shred -u "$archive_path"
  else
    rm -f "$archive_path"
  fi

  upload_path="$encrypted_path"
fi

echo "Removing backups older than $RETENTION_DAYS days..."
find "$backup_root_real" -maxdepth 1 -type f \( -name "menispot-backup-*.tar.gz" -o -name "menispot-backup-*.tar.gz.enc" \) -mtime +"$RETENTION_DAYS" -delete

if [ -n "$BACKUP_REMOTE" ]; then
  if command -v rclone >/dev/null 2>&1; then
    echo "Uploading backup to $BACKUP_REMOTE..."
    rclone copy "$upload_path" "$BACKUP_REMOTE"
  else
    echo "BACKUP_REMOTE is set, but rclone is not installed. Skipping remote upload."
  fi
fi

echo "Backup created:"
echo "$upload_path"
