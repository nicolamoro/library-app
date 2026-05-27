#!/bin/bash
set -euo pipefail

# ── System update ──────────────────────────────────────────────────────────────
dnf update -y

# ── Docker ────────────────────────────────────────────────────────────────────
dnf install -y docker
systemctl enable --now docker
usermod -aG docker ec2-user
# Pre-create ssm-user so the SSM agent reuses it with the docker group already set
useradd -m ssm-user 2>/dev/null || true
usermod -aG docker ssm-user

# ── Docker Compose v2 plugin ──────────────────────────────────────────────────
COMPOSE_VERSION="v2.27.0"
mkdir -p /usr/local/lib/docker/cli-plugins
curl -SL "https://github.com/docker/compose/releases/download/${COMPOSE_VERSION}/docker-compose-linux-x86_64" \
  -o /usr/local/lib/docker/cli-plugins/docker-compose
chmod +x /usr/local/lib/docker/cli-plugins/docker-compose

# ── Swap (2 GB) ───────────────────────────────────────────────────────────────
# SQL Server on a 1 GB instance needs swap to avoid OOM kills.
fallocate -l 4G /swapfile
chmod 600 /swapfile
mkswap /swapfile
swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab

# Prefer swapping over OOM-killing processes; keep filesystem cache hot.
echo 'vm.swappiness=60' >> /etc/sysctl.conf
echo 'vm.vfs_cache_pressure=50' >> /etc/sysctl.conf
sysctl -p

# ── App directory ─────────────────────────────────────────────────────────────
mkdir -p /opt/library-app/db-init
chown -R ec2-user:ec2-user /opt/library-app

# ── Systemd service: restart compose stack on reboot ─────────────────────────
cat > /etc/systemd/system/library-app.service << 'EOF'
[Unit]
Description=Library App Docker Compose
Requires=docker.service
After=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
User=ec2-user
WorkingDirectory=/opt/library-app
ExecStart=/usr/local/lib/docker/cli-plugins/docker-compose -f docker-compose.prod.yml up -d
ExecStop=/usr/local/lib/docker/cli-plugins/docker-compose -f docker-compose.prod.yml down
TimeoutStartSec=300

[Install]
WantedBy=multi-user.target
EOF

systemctl enable library-app.service
