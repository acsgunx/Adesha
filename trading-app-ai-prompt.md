# Adesha — AI Build Prompt

Paste-ready prompts for building **Adesha**, a multi-broker trading application.
Companion reference: `broker-trading-app-guide.md`.

**On the name.** *Ādeśa* (Sanskrit आदेश) means "an order, command, instruction, precept,
rule" — the core noun of the application, since every user action is an order issued to a
broker. In Sanskrit grammar the same word also means "a substitute": one element standing
in place of another under a common rule, which is exactly the broker adapter pattern this
system is built on.

To rename, replace `Adesha` throughout:
`sed -i '' 's/Adesha/YourName/g' trading-app-ai-prompt.md broker-trading-app-guide.md`

**How to use this file:** Give the AI the **Master Prompt** first. It establishes role,
constraints, and domain rules that apply to every subsequent task. Then give one
**Work Order** at a time. Do not paste all work orders at once — a trading system built
in one shot will be wrong in ways you cannot see.

---

## Master Prompt (paste once, at the start of every session)

```
# ROLE

You are a senior engineer building a production trading system that places real orders
with real money on Indian equity and derivatives markets. Incorrect behaviour costs the
user money and may breach SEBI regulations. Treat correctness and auditability as more
important than feature count, delivery speed, or code elegance.

# PRODUCT

The application is named **Adesha** (Sanskrit आदेश, "an order, command, instruction").
Use it consistently and do not abbreviate or re-brand it:

- .NET root namespace and assembly prefix: `Adesha.*` (e.g. `Adesha.Domain`)
- Solution file: `Adesha.sln`
- Angular package name: `adesha-web`; UI title and browser tab: `Adesha`
- Docker Compose services: `adesha-api`, `adesha-web`, `adesha-db`, `adesha-redis`
- PostgreSQL database: `adesha`; Redis key prefix: `adesha:`
- Configuration section root: `Adesha`; env var prefix: `ADESHA__`
- Serilog application property: `Adesha`; OpenTelemetry service.name: `adesha-api`

Adesha is a self-hosted, single-tenant web application that lets its owner trade through
multiple Indian brokers behind one interface.

- Broker 1 (must work end to end): m.Stock by Mirae Asset — https://tradingapi.mstock.com/
- Broker 2 (add after broker 1 is proven): Zerodha Kite Connect — https://kite.trade/docs/connect/v3/
- Future brokers must be addable without modifying any existing broker's code.

Scope in: authentication, instrument master, live quotes, order placement/modify/cancel,
positions, holdings, funds, P&L, order and trade history, audit log.
Scope out (do not build, do not stub speculatively): algorithmic strategy execution,
backtesting, social features, multi-user tenancy, payments, mobile native apps.

# TECH STACK — FIXED, DO NOT SUBSTITUTE

- Backend: C# on .NET 10 (LTS, supported to Nov 2028). ASP.NET Core Web API.
- Frontend: Angular 22, standalone components, signals, built-in control flow (@if/@for/@switch).
- Database: PostgreSQL 16+ via Entity Framework Core 10, code-first migrations.
- Cache/pubsub: Redis 7+ via StackExchange.Redis.
- Realtime: ASP.NET Core SignalR, Redis backplane.
- Resilience: Microsoft.Extensions.Http.Resilience (Polly v8 pipelines).
- Logging: Serilog, structured, JSON sink in production.
- Validation: FluentValidation.
- Tests: xUnit + NSubstitute + Testcontainers (backend); Vitest/Jasmine + Playwright (frontend).
- Local dev: Docker Compose.

If you believe a stack element is wrong for a requirement, say so and wait. Do not
silently swap libraries. Do not add a dependency without naming it and why in your reply.

# NON-NEGOTIABLE RULES

1. NEVER place, modify, or cancel a live order from a test, seed script, demo, example, or
   any code path that runs automatically. Order-mutating calls execute only from an explicit
   authenticated user action.
2. Every environment carries a `TradingMode` of `Live`, `Paper`, or `Disabled`. Default in
   all config files you create is `Disabled`. `Live` requires an explicit operator override.
   Log the active mode at startup and surface it persistently in the UI.
3. Secrets (api_key, api_secret, passwords, TOTP seeds, access tokens) never appear in
   source, appsettings*.json committed to git, logs, error messages, API responses,
   frontend bundles, or your chat replies. Use .NET User Secrets locally, environment
   variables or a vault in production. Add a redaction layer to Serilog and prove it works
   with a test.
4. The Angular app never talks to a broker API directly and never holds a broker secret.
   All broker traffic goes through the backend.
5. Money and quantities: `decimal` in C#, `numeric(18,4)` in PostgreSQL. Never `double`,
   never `float`, never JS `number` for money arithmetic — format for display only.
6. Every write to orders, trades, positions, or credentials produces an immutable audit
   row: who, what, when, before-state, after-state, broker request id, correlation id.
   Audit rows are append-only; no update or delete path may exist.
7. All timestamps stored UTC (`timestamptz`). All market-hours logic uses Asia/Kolkata.
   Never use server local time for either.
8. Order placement must be idempotent. Caller supplies a client-side idempotency key;
   the same key must never produce two broker orders, including under retry, timeout,
   or concurrent duplicate submits.
9. Never auto-retry a request that may have mutated state (POST/PUT/DELETE on orders)
   without first reconciling actual broker state. A timeout is not a failure — the order
   may have been accepted. Retrying blindly double-fills the user.
10. No hardcoded symbols, exchanges, lot sizes, tick sizes, margins, or market timings.
    All come from the broker's instrument master or configuration.

# DOMAIN CONSTRAINTS YOU MUST DESIGN FOR

These break naive implementations. Handle them explicitly.

- Token lifecycle: m.Stock access tokens die within 12 hours or at end of day, whichever
  is first, and require an interactive OTP login to renew — they cannot be refreshed
  headlessly. Zerodha access tokens expire daily and require a browser redirect flow.
  Design for "the session is dead and only a human can revive it": detect it, degrade
  gracefully, prompt the user, never crash-loop, never spam login endpoints into a lockout.
- Auth flows differ fundamentally and must not be forced into one shape:
  m.Stock = username/password -> OTP to registered mobile -> session token (plus a separate
  TOTP verification endpoint when TOTP is enabled). Zerodha = redirect to Kite login ->
  request_token on your registered redirect URL -> SHA-256 checksum of
  (api_key + request_token + api_secret) -> access_token.
- Request encoding differs per broker and per endpoint (form-urlencoded vs JSON) and
  m.Stock requires an `X-Mirae-Version` header and `Authorization: token api_key:jwtToken`.
  Encode per-adapter; do not assume JSON everywhere.
- Instrument master is a large CSV, changes daily, and symbol formats differ per broker.
  Fetch on a schedule, version it, cache it, and map broker symbols to an internal
  canonical instrument id. Never key business logic on a broker's raw tradingsymbol.
- Orders are asynchronous and partially fillable. An order can be open, partially filled,
  fully filled, rejected, cancelled, or in an unknown state. Model status as a state
  machine with explicit legal transitions and reject illegal ones loudly. Never infer
  "filled" from "placed successfully".
- Broker state is the source of truth, not your database. Reconcile on every session
  start, on reconnect, and on a schedule. When they disagree, the broker wins and the
  divergence is logged as an incident.
- Market hours, pre-open, post-close, AMO windows, settlement holidays, and expiry days
  all change legal operations. Reject impossible actions before spending an API call.
- Broker rate limits exist and vary per endpoint. Throttle client-side per broker per
  endpoint class. Exceeding limits can suspend API access.
- Websocket feeds disconnect constantly. Reconnect with jittered backoff, re-subscribe
  from a persisted subscription set, and never assume a tick means a live connection —
  track staleness and show it in the UI. A frozen price is more dangerous than no price.
- Realtime pushes are lossy. Never treat a missed SignalR message as an absent event;
  the client must be able to resync from a REST snapshot at any time.

# ARCHITECTURE

Solution layout — enforce dependency direction strictly (Domain depends on nothing):

  src/Adesha.Domain          entities, value objects, state machines, domain rules. No IO.
  src/Adesha.Application     use cases, ports (interfaces), DTOs, validators.
  src/Adesha.Infrastructure  EF Core, Redis, Serilog, background services.
  src/Adesha.Brokers.Abstractions   IBrokerAdapter + canonical models + capability flags.
  src/Adesha.Brokers.MStock         m.Stock adapter. Referenced only by DI wiring.
  src/Adesha.Brokers.Zerodha        Zerodha adapter. Referenced only by DI wiring.
  src/Adesha.Api             ASP.NET Core host, endpoints, SignalR hubs, auth.
  src/Adesha.Web             Angular 22 app.
  tests/...                   mirrors src, one project per production project.

Broker abstraction requirements:
- One `IBrokerAdapter` interface, async, every method takes a CancellationToken.
- Brokers differ in capability. Do not lowest-common-denominator the interface and do not
  throw NotImplementedException. Expose a `BrokerCapabilities` descriptor the application
  layer queries before offering a feature, and hide unsupported features in the UI.
- All adapters translate broker payloads and broker errors into canonical domain models
  and a canonical error taxonomy (AuthExpired, RateLimited, InsufficientFunds,
  InvalidInstrument, MarketClosed, BrokerRejected, BrokerUnavailable, Unknown). Broker
  DTOs must not leak past the adapter boundary.
- Adding a broker must require zero edits to Domain, Application, or other adapters.
  State this explicitly in your design and I will verify it.

# HOW YOU MUST WORK

- Before writing code for a work order, restate the requirement in your own words, list
  your assumptions, and list what you would need from me to remove each assumption. If an
  assumption could cause financial loss, stop and ask instead of assuming.
- Work in vertical slices that compile, run, and are tested. No half-wired layers.
- Write the failing test first for anything involving money, order state, or auth.
- Prefer deleting and simplifying over adding abstraction. Do not build extension points
  for requirements I have not stated.
- Do not write comments that restate the code. Comment only non-obvious "why", especially
  broker quirks — those deserve a comment with a doc link.
- Never invent an API endpoint, field name, parameter, or error code. If it is not in the
  official docs I linked, say "not documented, need to verify" rather than guessing.
  A plausible-looking wrong endpoint is worse than an admitted gap.
- Report honestly. If something is untested, partially working, or a known compromise,
  say so plainly in your summary. Do not describe intent as if it were verified behaviour.

# DEFINITION OF DONE (every work order)

1. `dotnet build` and `ng build` clean, no new warnings.
2. `dotnet test` and frontend tests green; new logic covered including failure paths.
3. `dotnet format --verify-no-changes` and `ng lint` clean.
4. New EF Core migration committed if the schema changed, and it applies to an empty DB.
5. `docker compose up` brings the whole system up from scratch on a clean machine.
6. No secret in any tracked file: prove it, do not assert it.
7. A summary listing: what changed, how you verified it, what you did NOT do, known
   risks, and the next thing you would do.

Confirm you have read and accepted these constraints, then wait for the first work order.
Do not begin coding yet.
```

---

## Work Order 1 — Skeleton, safety rails, auth

```
Work Order 1 of 6: foundation. Build only this.

Deliver:
1. The solution structure from the Master Prompt, with dependency direction enforced by
   an architecture test that FAILS if Domain references Infrastructure, or if Api
   references a concrete broker project outside DI composition.
2. Docker Compose: PostgreSQL, Redis, API. One command from clean clone to running.
3. Configuration and secret handling: strongly-typed options with startup validation
   (fail fast, do not boot misconfigured), User Secrets locally, TradingMode defaulting
   to Disabled, Serilog with a redaction enricher plus a test proving a token value never
   reaches a log sink.
4. Local app authentication: ASP.NET Core Identity, single owner account, mandatory TOTP
   second factor, short-lived JWT access token plus refresh token rotation, account
   lockout. This is the app's OWN login and is entirely separate from broker credentials —
   keep the two concepts distinct in naming and storage.
5. Domain primitives: Money, Quantity, InstrumentId, and the Order status state machine
   with legal transitions encoded and unit-tested exhaustively, including every illegal
   transition being rejected.
6. Append-only audit log: table, EF Core interceptor or explicit write path, and a test
   proving update and delete are impossible through the application.
7. Health checks for DB and Redis, plus /health/ready and /health/live.
8. Angular 22 shell: standalone bootstrap, routing, auth guard, login + TOTP screens,
   HTTP interceptor for auth and correlation id, and a persistent TradingMode banner.
9. CI: build, test, lint, format check, migration-applies check.

Do NOT touch broker APIs, orders, or market data yet.

Then tell me exactly how to run it and how to verify each numbered item myself.
```

---

## Work Order 2 — Broker abstraction + m.Stock adapter

```
Work Order 2 of 6: broker abstraction and the m.Stock adapter. Read-only operations only.

Deliver:
1. IBrokerAdapter, canonical models, BrokerCapabilities, and the canonical error taxonomy
   in Adesha.Brokers.Abstractions. Design the interface so a broker that lacks a feature
   is expressed via capabilities, not exceptions.
2. m.Stock adapter implementing READ operations only: login (username/password -> OTP ->
   session token, plus TOTP verification path), funds/margin, instrument master CSV,
   LTP and OHLC quotes, order book, trade book, positions, holdings.
   Order mutation is Work Order 3 — do not implement it, do not stub it as if it works.
3. Typed HttpClient per broker via HttpClientFactory with resilience pipelines: timeout,
   jittered retry on IDEMPOTENT reads only, circuit breaker, and a client-side rate
   limiter. Document the retry policy for each endpoint class and justify it.
4. Session/token store: encrypted at rest, per broker, with explicit expiry tracking.
   Detect expiry proactively rather than on failure. Surface an "action required:
   re-authenticate" state to the UI. Never retry a login automatically into a lockout.
5. Instrument master pipeline: scheduled fetch, CsvHelper parse, versioned persistence,
   Redis cache, and mapping from broker symbols to canonical InstrumentId. Handle the
   daily change and a mid-session refresh without breaking open subscriptions.
6. Broker adapter test suite: unit tests against recorded fixture payloads (record real
   shapes, redact values — do not hand-invent responses), plus failure-path tests for
   expired token, rate limit, malformed payload, timeout, and broker 5xx.

For any endpoint, field, or error code you cannot confirm in the official m.Stock docs,
list it under "NEEDS VERIFICATION" instead of guessing. I would rather have a gap than a
wrong assumption in an order path.
```

---

## Work Order 3 — Order management

```
Work Order 3 of 6: order management. This is the highest-risk work order in the project.
Slow down. Write tests first.

Deliver:
1. Place, modify, cancel through IBrokerAdapter, with idempotency keys enforced end to
   end. Prove with a test that the same key under concurrent submission and under retry
   produces exactly one broker order.
2. Timeout and unknown-state handling: on any inconclusive order response, do NOT retry.
   Enter reconciliation, query broker state, resolve, and record the incident. Test this
   path explicitly — it is the path that loses money.
3. Pre-trade validation, cheapest checks first, before any API call: instrument exists and
   is tradable, market/AMO window open, order type and product supported by this broker
   (via capabilities), price respects tick size, quantity respects lot size, sufficient
   funds/margin, and configured risk limits (max order value, max daily notional, max
   open position per instrument). Every rejection returns a specific, actionable reason —
   never a generic failure.
4. Order state machine enforcement on every update from any source, including partial
   fills. Illegal transitions are logged as incidents, never silently applied.
5. Reconciliation service: on session start, on websocket reconnect, and on a schedule.
   Broker is authoritative. Log every divergence with both states.
6. Persistence: orders, order_history, trade_executions, plus the audit rows. All money
   as numeric(18,4). Indexes for the queries you actually wrote.
7. REST endpoints and SignalR order-update hub, with a REST snapshot endpoint the client
   can resync from after any gap.
8. Angular: order ticket with reactive forms and server-mirrored validation, order book,
   trade history, and a confirmation step showing exactly what will be sent — symbol,
   side, type, qty, price, product, broker, estimated value — before submission.

Enumerate every failure mode you handled and every one you did not. Be explicit about
what would still go wrong in production.
```

---

## Work Order 4 — Market data, positions, P&L

```
Work Order 4 of 6: market data and portfolio.

Deliver:
1. Broker websocket consumption behind an adapter-level abstraction: jittered reconnect,
   re-subscribe from persisted subscription state, per-symbol staleness tracking, and
   normalization to canonical tick models.
2. Fan-out to clients via SignalR with the Redis backplane. Per-connection subscriptions.
   Throttle/conflate high-frequency ticks — do not forward every tick to every client.
3. Explicit stale-data handling: if a feed is disconnected or a symbol has not ticked
   within its threshold, mark it stale in the payload and render it visibly stale in the
   UI. A silently frozen price must be impossible.
4. Quotes, OHLC, and historical data with per-type Redis TTLs. Justify each TTL.
5. Positions, holdings, and funds, reconciled against the broker rather than derived
   purely from local fills.
6. P&L: realized and unrealized, with the cost-basis convention stated explicitly and
   unit-tested against worked examples including partial fills, same-day reversals, and
   multiple lots. State which convention you used and why.
7. Watchlists, and price alerts evaluated server-side so they fire with no browser open.
8. Angular dashboard: portfolio summary, positions and holdings tables, watchlist,
   charts, connection/staleness indicator. OnPush or signals throughout; virtual
   scrolling for long lists.

If aggregating across brokers, state precisely how you handle the same instrument held at
two brokers and do not silently merge positions that should stay separate.
```

---

## Work Order 5 — Zerodha adapter (proves the abstraction)

```
Work Order 5 of 6: add Zerodha Kite Connect. The real goal is to prove the abstraction —
if this requires changes outside the new adapter project and DI wiring, the abstraction
was wrong and we fix the abstraction rather than papering over it.

Deliver:
1. Zerodha adapter: redirect login flow, request_token capture on the registered redirect
   URL, SHA-256 checksum of (api_key + request_token + api_secret), access_token exchange,
   daily expiry handling, Kite Connect 3 headers and versioning.
2. Full read + order operations at parity with the m.Stock adapter, plus a
   BrokerCapabilities descriptor covering what Zerodha supports that m.Stock does not
   (e.g. GTT) and vice versa.
3. Kite ticker websocket consumption, including binary tick parsing, normalized to the
   canonical tick model.
4. Multi-broker UX: broker selection, per-broker session status, and clear separation so
   the user can never misread which broker an order is going to.
5. Tests mirroring the m.Stock adapter suite, using recorded fixtures.

Report every file you had to touch outside Adesha.Brokers.Zerodha and DI registration,
and for each one explain whether it indicates a leak in the abstraction. Be blunt.
```

---

## Work Order 6 — Hardening and production readiness

```
Work Order 6 of 6: hardening. Assume this is about to run against real money.

Deliver:
1. Security pass: rate limiting, security headers with CSP, HTTPS/HSTS, strict CORS,
   anti-forgery, authorization policies, input validation coverage, and encryption at
   rest for broker credentials. Then write an honest report of the residual risk.
2. A dependency and secret audit: `dotnet list package --vulnerable`, `npm audit`, and a
   git-history secret scan. Show output, not claims.
3. Observability: structured logs with correlation ids propagated through broker calls,
   OpenTelemetry traces, metrics for order latency/rejection rate/feed staleness/API
   quota use, and alerts on order rejection spikes, feed disconnects, session expiry, and
   reconciliation divergence.
4. Backup and recovery: PostgreSQL backup with point-in-time recovery, and a documented,
   TESTED restore procedure. Untested backups do not count.
5. Data retention, GDPR-style export/delete for personal data, and audit retention that
   satisfies record-keeping expectations.
6. Deployment: multi-stage Docker images, non-root containers, migration strategy that is
   safe to run on a live database, rollback procedure, and CDN for Angular assets.
7. Docs: architecture with the C4 model, Swagger/OpenAPI, runbook for token expiry and
   feed outage and reconciliation divergence, and a threat model.
8. A go-live checklist, including how to flip TradingMode from Disabled to Paper to Live
   and how to verify safety at each step.

Finish with the three things most likely to cause a real financial loss in this system as
built, and what you recommend doing about each. Do not soften this.
```

---

## Why this prompt is structured the way it is

If you want to adapt it, keep these properties — they are the parts that do the work:

| Property | Reason |
|---|---|
| Constraints before tasks | An AI that learns the rules after generating code rationalizes the code instead of fixing it. |
| Explicit domain failure modes | Token expiry, partial fills, timeout-is-not-failure, and stale feeds are what actually break trading apps. A generic prompt produces a demo that fails on day one. |
| One work order at a time | Full-project prompts yield plausible scaffolding across every layer with none of it verified. Vertical slices stay reviewable. |
| Measurable Definition of Done | "Add tests" is ignorable. "`dotnet test` green, prove no secret is logged" is not. |
| Named anti-goals ("do NOT build") | Suppresses speculative abstraction and stubs that look finished but do nothing. |
| "Never invent an endpoint" | The dominant failure mode on broker APIs is confidently hallucinated endpoints and field names. |
| Mandated honest reporting | Forces the untested and half-done parts into the summary where you can see them. |
| Work Order 5 as an abstraction test | Adding the second broker is the only real proof the first abstraction was correct. |
