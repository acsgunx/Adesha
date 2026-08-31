# Adesha — Development Scripts & Commands

All commands used during development, organized by Work Order. Run from the repo root
(`/Users/cgunnam/git/acsgunx/Adesha`) unless noted otherwise.

## Quick Start (single command)

```bash
./dev.sh              # kill + clean + build + run (full reset)
./dev.sh --no-clean   # kill + build + run (skip clean, faster)
./dev.sh --run-only   # kill + run only (no rebuild)
./dev.sh --kill-only  # just kill everything, don't build or run
```

---

## Tooling Setup (one-time)

```bash
# Docker runtime (macOS via Colima)
brew install colima docker
colima start

# .NET 10 SDK
brew install dotnet

# Aspire CLI
brew install aspire
# or: dotnet tool install -g aspire.cli
# verify: aspire --version

# Angular CLI (local to project, not global)
cd src/Adesha.Web && npm install -g @angular/cli@22
```

---

## Work Order 1 — Skeleton, Safety Rails, Auth

### Build & Test

```bash
# Build the full solution
dotnet build

# Run all tests
dotnet test

# Format check (must be clean)
dotnet format --verify-no-changes

# Angular lint + build
cd src/Adesha.Web
npm run lint
npm run build
cd ../..

# Migration applies to empty DB (CI check)
docker run -d --name adesha-migrate-check \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=adesha_migrate_check \
  -p 5433:5432 postgres:16-alpine
sleep 4
ADESHA_MIGRATIONS_CONNECTION='Host=localhost;Port=5433;Database=adesha_migrate_check;Username=postgres;Password=postgres' \
  dotnet ef database update \
    --project src/Adesha.Infrastructure \
    --startup-project src/Adesha.Infrastructure
docker stop adesha-migrate-check && docker rm adesha-migrate-check
```

### Run the App

```bash
# Start the full stack (Postgres, Redis, API, Angular)
aspire run

# Verify health
curl http://localhost:5157/health     # → Healthy
curl http://localhost:5157/alive      # → Healthy
curl http://localhost:4200/           # → Angular app HTML

# Check system status (TradingMode should be Disabled)
curl http://localhost:5157/api/system/status
curl http://localhost:5157/api/system/setup-required
```

### Architecture Tests

```bash
# Verify dependency direction is enforced
dotnet test tests/Adesha.Architecture.Tests/Adesha.Architecture.Tests.csproj
```

### User Secrets (local dev secrets — never committed)

```bash
cd src/Adesha.Api
dotnet user-secrets init
dotnet user-secrets set "Adesha:Jwt:SigningKey" "<your-signing-key>"
dotnet user-secrets set "MStock:ApiKey" "<your-mstock-api-key>"
cd ../..
```

---

## Work Order 2 — Broker Abstraction + m.Stock Read-Only Adapter

### Build & Test

```bash
# Build the full solution (now includes Brokers.Abstractions + Brokers.MStock)
dotnet build

# Run all tests (152 tests across 6 projects)
dotnet test

# Run only the broker adapter tests (24 tests)
dotnet test tests/Adesha.Brokers.MStock.Tests/Adesha.Brokers.MStock.Tests.csproj

# Run only the architecture tests (15 tests, includes broker dependency checks)
dotnet test tests/Adesha.Architecture.Tests/Adesha.Architecture.Tests.csproj
```

### Verify Broker Wiring

```bash
# Start the app
aspire run

# The API now has MStock broker adapter registered via DI.
# Verify the API starts without errors (MStock:ApiKey must be in User Secrets):
curl http://localhost:5157/health     # → Healthy

# Check Aspire dashboard for:
# - No broker credentials in trace attributes (Rule 3 redaction)
# - HTTP requests to api.mstock.trade show redacted Authorization header
```

### m.Stock API Reference (used during development)

```bash
# Official docs (fetched during development to verify endpoints):
# https://tradingapi.mstock.com/docs/v1/typeA/User/
# https://tradingapi.mstock.com/docs/v1/typeA/Orders/
# https://tradingapi.mstock.com/docs/v1/typeA/Portfolio/
# https://tradingapi.mstock.com/docs/v1/typeA/Position/
# https://tradingapi.mstock.com/docs/v1/typeA/market-quote-and-instrument/

# Root endpoint: https://api.mstock.trade/openapi/typea
# Required headers:
#   X-Mirae-Version: 1
#   Authorization: token api_key:jwtToken
#   Content-Type: application/x-www-form-urlencoded (for POST endpoints)
```

---

## Work Order 3 — Order Management (pending)

```bash
# Commands will be added here as WO3 is implemented.
```

---

## Work Order 4 — Market Data, Positions, P&L (pending)

```bash
# Commands will be added here as WO4 is implemented.
```

---

## Work Order 5 — Zerodha Adapter (pending)

```bash
# Commands will be added here as WO5 is implemented.
```

---

## Work Order 6 — Hardening & Production Readiness (pending)

```bash
# Commands will be added here as WO6 is implemented.
```

---

## Common Debugging Commands

```bash
# Check what's running on Adesha ports
lsof -iTCP -sTCP:LISTEN -P -n | grep -E 'Adesha|5157|4200|aspire'

# Check Docker containers (Aspire-managed)
docker ps --filter "name=adesha-" --format "{{.Names}}\t{{.Status}}"

# Kill any stuck Adesha processes manually
pkill -f "aspire run"
pkill -f "Adesha.Api"
pkill -f "Aspire.Dashboard"
pkill -f "dcp run-controllers"
docker ps --filter "name=adesha-" -q | xargs -r docker stop
docker ps --filter "name=adesha-" -q | xargs -r docker rm

# View Aspire DCP logs (if startup fails)
ls /var/folders/tl/*/T/aspire-*/
cat /var/folders/tl/*/T/aspire-*/resource-*.log

# Check EF Core migrations
dotnet ef migrations list --project src/Adesha.Infrastructure --startup-project src/Adesha.Infrastructure

# Add a new migration (when schema changes)
dotnet ef migrations add <MigrationName> \
  --project src/Adesha.Infrastructure \
  --startup-project src/Adesha.Infrastructure

# Apply migrations manually
dotnet ef database update \
  --project src/Adesha.Infrastructure \
  --startup-project src/Adesha.Infrastructure
```

---

## CI Pipeline (GitHub Actions)

The CI workflow is at `.github/workflows/ci.yml` and runs on every push/PR:

- **Backend job**: `dotnet restore` → `dotnet format --verify-no-changes` → `dotnet build` → `dotnet test` → migration-applies check (spins up Postgres in Docker)
- **Frontend job**: `npm ci` → `npm run lint` → `npm run build`
