# Security Notes

This project is designed so production secrets and customer data stay outside the public repository.

## Secrets

Never commit:

- `.env.production`
- database passwords
- JWT signing keys
- administrator passwords
- SMTP or email provider credentials
- cloud storage tokens
- SSH keys
- `rclone.conf`

Only placeholder example files should be public.

## Production Database

PostgreSQL should stay private:

- do not publish port `5432` from `docker-compose.prod.yml`
- keep PostgreSQL inside the Docker network
- use a strong production database password
- keep `.env.production` readable only by root or the deployment user

Recommended permission on the VPS:

```bash
chmod 600 /opt/menispot/.env.production
```

## Backups

Backups may contain real customer data and uploaded images.

Keep backups:

- outside `docs/`, `frontend/`, `backend/`, `deploy/`, and `.git/`
- outside GitHub
- readable only by the server user that needs them
- encrypted before cloud upload when possible

Recommended permissions:

```bash
chmod 700 /opt/menispot/backups
chmod 600 /opt/menispot/backups/menispot-backup-*.tar.gz*
```

## Server Access

Recommended VPS hardening:

- allow only ports `22`, `80`, and `443` through UFW
- use SSH keys instead of password login
- disable direct PostgreSQL access from the internet
- keep Docker, Ubuntu packages, and app dependencies updated
- store GitHub deploy keys with minimum required access

## If A Secret Leaks

If a real password, JWT key, SSH key, or cloud token is accidentally committed or exposed:

1. Rotate the leaked secret immediately
2. Update the VPS `.env.production`
3. Restart the production containers
4. Revoke exposed SSH/cloud/API keys
5. Remove the secret from Git history if the repository is public

Do not rely only on deleting the visible file from GitHub. Once a secret is pushed, treat it as compromised.
