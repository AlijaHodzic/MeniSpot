# Production Backups

MeniSpot production backups should be created on the VPS and stored outside the Git repository.

The backup script is located at:

```bash
deploy/backup-prod.sh
```

It creates one compressed archive that contains:

- PostgreSQL database dump
- Uploaded images from `/app/wwwroot/uploads`
- Backup manifest

It does not include `.env.production`, SSH keys, Docker images, or other machine secrets.

The script also applies safety checks:

- backup directory permissions are set to owner-only access
- backup archives are created with `600` permissions
- backups are refused inside `.git`, `docs`, `frontend`, `backend`, or `deploy`
- optional encrypted backup archives are supported for cloud upload
- old local backup archives are removed after the retention window

## Run A Manual Backup

SSH into the VPS and run:

```bash
cd /opt/menispot
chmod +x deploy/backup-prod.sh
./deploy/backup-prod.sh
```

Backups are saved to:

```text
/opt/menispot/backups
```

## Optional Settings

You can override defaults when running the script:

```bash
RETENTION_DAYS=30 BACKUP_ROOT=/opt/menispot/backups ./deploy/backup-prod.sh
```

Available settings:

| Variable | Default | Purpose |
| --- | --- | --- |
| `PROJECT_DIR` | `/opt/menispot` | Production project directory |
| `COMPOSE_FILE` | `docker-compose.prod.yml` | Docker Compose production file |
| `ENV_FILE` | `.env.production` | Production environment file |
| `BACKUP_ROOT` | `/opt/menispot/backups` | Where backup archives are stored |
| `RETENTION_DAYS` | `14` | Deletes local backup archives older than this |
| `BACKUP_REMOTE` | empty | Optional rclone destination |
| `BACKUP_ENCRYPTION_PASSWORD` | empty | Optional password for encrypted `.enc` backups |

## Optional Cloud Upload

If `rclone` is configured on the VPS, the script can upload backups to a cloud folder.

Example:

```bash
BACKUP_REMOTE="gdrive:MeniSpotBackups" ./deploy/backup-prod.sh
```

For cloud upload, encrypted backups are recommended:

```bash
BACKUP_ENCRYPTION_PASSWORD="use-a-long-random-password" BACKUP_REMOTE="gdrive:MeniSpotBackups" ./deploy/backup-prod.sh
```

Store the encryption password somewhere safe, outside the repository and outside the VPS. Without that password, encrypted backups cannot be restored.

Do not store production backups in the `docs/` folder or commit them to GitHub. Backups can contain customer data and uploaded files.

## Automatic Nightly Backup

Open root crontab on the VPS:

```bash
crontab -e
```

Run backup every night at `03:15`:

```cron
15 3 * * * cd /opt/menispot && /opt/menispot/deploy/backup-prod.sh >> /opt/menispot/backups/backup.log 2>&1
```

With cloud upload:

```cron
15 3 * * * cd /opt/menispot && BACKUP_REMOTE="gdrive:MeniSpotBackups" /opt/menispot/deploy/backup-prod.sh >> /opt/menispot/backups/backup.log 2>&1
```

With encrypted cloud upload:

```cron
15 3 * * * cd /opt/menispot && BACKUP_ENCRYPTION_PASSWORD="use-a-long-random-password" BACKUP_REMOTE="gdrive:MeniSpotBackups" /opt/menispot/deploy/backup-prod.sh >> /opt/menispot/backups/backup.log 2>&1
```

If you do not want the encryption password stored directly in crontab, place it in a root-only file such as `/root/menispot-backup.env`, set file permissions to `600`, and source it from the cron command.

## Download Backup To Local PC

From Windows PowerShell:

```powershell
scp root@YOUR_SERVER_IP:/opt/menispot/backups/menispot-backup-YYYYMMDD-HHMMSS.tar.gz "$env:USERPROFILE\Desktop\"
```

Replace the file name with the real backup archive name.

## Restore Notes

Keep restore testing separate from production. A backup is only useful if it can be restored.

High-level restore flow:

1. Copy the backup archive to a safe machine or test VPS
2. Extract the archive
3. Restore `database.sql` into PostgreSQL
4. Extract `uploads.tar.gz` into the API uploads directory or Docker volume
5. Start the application and confirm the public menu loads

## Never Commit These Files

The repository ignores common backup and secret file patterns, but always double-check before committing:

- `.env.production`
- `backups/`
- `*.tar.gz`, `*.sql`, `*.dump`, `*.backup`, `*.enc`
- SSH keys such as `id_ed25519`, `id_rsa`, `*.pem`
- `rclone.conf`
- real database, JWT, SMTP, or cloud credentials
