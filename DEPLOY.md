# Deployment Guide

This app runs on a single EC2 t2.micro instance (AWS Free Tier) using Docker Compose. The app image is stored in ECR; the PostgreSQL container uses the official `postgres:16-alpine` base image with init scripts mounted from S3.

## Architecture overview

```
git push origin main
  └─ ci.yml  →  ghcr.io/<owner>/library-app:sha-<hash>

git push tag vX.Y.Z
  └─ cd.yml
       ├─ Promotes image: GHCR sha-hash → GHCR vX.Y.Z + latest → ECR vX.Y.Z + latest
       ├─ Uploads DB init scripts + docker-compose.prod.yml to S3
       └─ SSM send-command → EC2
            ├─ docker login to ECR (via IAM role, no credentials stored on EC2)
            ├─ syncs DB init scripts from S3 → /opt/library-app/db-init/
            ├─ downloads docker-compose.prod.yml from S3
            ├─ writes .env (secrets from SSM Parameter Store)
            └─ docker compose up -d

AWS resources (all tagged Project=library-app):
  EC2 t2.micro         — Blazor Server app + PostgreSQL 16 containers
  ECR repository       — library-app image only (~200-300 MB, within free tier)
  EBS 20 GB gp2        — root volume
  Elastic IP           — static public IP
  Security Group       — TCP 8080 open; no port 22 (SSM used)
  IAM instance profile — SSM core + ECR read + SSM params + S3 db-init read
  S3 bucket            — Terraform state + DB init scripts + compose file
  DynamoDB table       — Terraform state lock
  SSM Parameters       — /library-app/{db-password, ecr-registry, s3-bucket, app-version}
```

## GitHub configuration required

**Secrets** (Settings → Secrets and variables → Actions → Secrets):

| Secret | When you get it | Source |
|--------|-----------------|--------|
| `AWS_ACCESS_KEY_ID` | Prerequisite | AWS Console → IAM → Create access key |
| `AWS_SECRET_ACCESS_KEY` | Prerequisite | Same step |
| `DB_PASSWORD` | Step 1 | Your choice — strong password |
| `TF_BACKEND_BUCKET` | Step 2 | `infra.yml bootstrap` step summary |
| `TF_BACKEND_TABLE` | Step 2 | `infra.yml bootstrap` step summary |
| `EC2_INSTANCE_ID` | Step 3 | `infra.yml apply` step summary |

**Variables** (Settings → Secrets and variables → Actions → Variables):

| Variable | When you get it | Source |
|----------|-----------------|--------|
| `AWS_REGION` | Prerequisite | Your choice (e.g. `eu-west-1`) |

## First-time setup

### Prerequisite — Create an IAM user

**Option A — AWS Console:**
IAM → Users → Create user → Attach policy `AdministratorAccess` → Security credentials → Create access key → type: CLI.

**Option B — AWS CLI** (requires existing admin credentials):
```bash
aws iam create-user --user-name library-app-deploy

aws iam attach-user-policy \
  --user-name library-app-deploy \
  --policy-arn arn:aws:iam::aws:policies/AdministratorAccess

aws iam create-access-key --user-name library-app-deploy
# Save AccessKeyId and SecretAccessKey immediately
```

Save `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` as GitHub **secrets**, and `AWS_REGION` as a GitHub **variable** (it isn't sensitive and being unmasked makes log debugging easier).

---

### Step 1 — Set DB password

Choose a strong PostgreSQL password (min. 8 chars, uppercase + lowercase + digits + symbols).  
Save as GitHub secret: `DB_PASSWORD`.

---

### Step 2 — Bootstrap S3 + DynamoDB

> Actions → `infra.yml` → Run workflow → action: **bootstrap**

This creates:
- S3 bucket for Terraform state (versioned, encrypted, private)
- DynamoDB table for state locking

The step summary shows the names — save them as GitHub secrets: `TF_BACKEND_BUCKET`, `TF_BACKEND_TABLE`.

---

### Step 3 — Provision AWS infrastructure

> Actions → `infra.yml` → Run workflow → action: **apply**

This creates: EC2 instance, ECR repository, security group, Elastic IP, IAM role, and SSM parameters.

The step summary shows:

```
elastic_ip   = "1.2.3.4"
instance_id  = "i-0abc..."
ecr_registry = "123456789.dkr.ecr.eu-west-1.amazonaws.com"
```

Save `instance_id` as GitHub secret: `EC2_INSTANCE_ID`.

Note: the EC2 instance needs a few minutes to run the bootstrap user-data script (install Docker, configure swap). The first deploy should be done at least 3-4 minutes after the instance starts.

---

### Step 4 — First deploy

Ensure the tagged commit has already passed CI on `main`:

```bash
git tag v0.1.0
git push origin v0.1.0
```

This triggers `cd.yml`, which:
1. Promotes the GHCR image to ECR with the version tag
2. Uploads DB init scripts to S3
3. Deploys to EC2 via SSM

App available at `http://<elastic_ip>:8080` when the workflow completes.

---

## Deploying new versions

```bash
# After pushing commits to main and seeing CI pass:
git tag v1.2.3
git push origin v1.2.3
```

`cd.yml` handles the rest.

---

## CI / CD separation

| Workflow | Trigger | What it does |
|----------|---------|--------------|
| `ci.yml` | push to any branch, PRs to main | Build, test, docker validate; push `sha-<hash>` to GHCR on main |
| `cd.yml` | push of `v*.*.*` tag | Promote image, sync assets, deploy to EC2 |
| `infra.yml` | manual `workflow_dispatch` | `bootstrap` / `apply` / `destroy` infrastructure |

`latest` in GHCR and ECR is only updated by `cd.yml` — never by CI.

---

## Connecting to the instance (day-2 operations)

There is no SSH access. Use AWS Systems Manager Session Manager:

```bash
aws ssm start-session --target <EC2_INSTANCE_ID> --region <AWS_REGION>
```

Or via AWS Console: EC2 → select instance → Connect → Session Manager.

---

## Viewing logs

```bash
# Via SSM session:
cd /opt/library-app
docker compose -f docker-compose.prod.yml logs -f
docker compose -f docker-compose.prod.yml logs app --tail=100
docker compose -f docker-compose.prod.yml logs db  --tail=100
```

---

## Restarting the stack

```bash
# Via SSM session:
cd /opt/library-app
docker compose -f docker-compose.prod.yml restart

# Or individual service:
docker compose -f docker-compose.prod.yml restart app
```

---

## Tearing down all AWS resources

> Actions → `infra.yml` → Run workflow → action: **destroy**

This runs `terraform destroy` and deletes all SSM parameters. The S3 bucket and DynamoDB table (used for Terraform state) are not deleted by Terraform — remove them manually if no longer needed:

```bash
aws s3 rb s3://<TF_BACKEND_BUCKET> --force
aws dynamodb delete-table --table-name library-app-tflock --region <AWS_REGION>
```

---

## DB initialization

The `postgres:16-alpine` container automatically runs any scripts in `/docker-entrypoint-initdb.d` (the synced `db-init/` directory) **once, on first init** — i.e. only when the data directory is empty. It runs them in filename order: `01_schema.sql`, `02_procedures.sql`, `03_seed.sql`.

The `postgres-data` Docker volume persists across container restarts and redeployments. Init scripts only run on the very first container creation.

To reset the database (⚠️ data loss):
```bash
# Via SSM session:
cd /opt/library-app
docker compose -f docker-compose.prod.yml down -v   # -v removes the named volume
docker compose -f docker-compose.prod.yml up -d     # reinitialises from init scripts
```
