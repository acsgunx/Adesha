---
name: testing-auth-flow
description: How to run the Adesha API + Angular app locally and exercise the owner-setup / TOTP login flow end to end (first-run state, TOTP code generation, session persistence checks).
---

# Testing the Adesha auth flow locally

## Services

Postgres and Redis usually already run as docker containers `adesha-pg` (host port 55432,
`postgres`/`devpass`) and `adesha-redis` (host port 56379). Start them if stopped.

Config key names matter — read `src/Adesha.Api/Program.cs` before guessing:

- DB connection string key is **`adesha`** (not `adesha-db`): `ConnectionStrings__adesha`
- Redis: `ConnectionStrings__adesha-redis`
- JWT signing key (>= 32 chars, normally supplied by Aspire): `Adesha__Jwt__SigningKey`

Run the API (applies EF migrations automatically in Development):

```bash
export PATH=$HOME/.dotnet:$PATH
dotnet build src/Adesha.Api
env ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5157 \
  "ConnectionStrings__adesha=Host=localhost;Port=55432;Database=adesha;Username=postgres;Password=devpass" \
  "ConnectionStrings__adesha-redis=localhost:56379" \
  "Adesha__Jwt__SigningKey=dev-signing-key-dev-signing-key-dev-signing-key-0123456789" \
  dotnet run --project src/Adesha.Api --no-build --launch-profile http
```

Frontend: `source ~/.nvm/nvm.sh && nvm use 24 && cd src/Adesha.Web && npx ng serve --port 4200`
(`src/proxy.conf.mjs` proxies `/api` to `http://localhost:5157`, overridable with `ADESHA_API_URL`).

## Gotcha: stale API binary

If someone started the API with `dotnet run --no-build`, the running process can predate the
code under test and your results will be wrong (e.g. `/api/system/setup-required` returning the
old semantics). Always `pkill -f Adesha.Api`, rebuild, and restart the API yourself before testing,
and sanity-check one changed endpoint with curl before trusting UI behaviour.

## Genuine first-run state

```bash
docker exec adesha-pg psql -U postgres -c "DROP DATABASE IF EXISTS adesha WITH (FORCE);"
docker exec adesha-pg psql -U postgres -c "CREATE DATABASE adesha;"
# restart the API so migrations re-apply, then:
curl -s localhost:5157/api/system/setup-required   # {"setupRequired":true}
```

Inspect owner state directly:
`docker exec adesha-pg psql -U postgres -d adesha -c 'select "UserName","TwoFactorEnabled" from "AspNetUsers";'`

## TOTP codes

`sudo apt-get install -y oathtool`, then `oathtool --totp -b <SHAREDKEY>` using the base32 key the
/setup page displays. Codes are valid ~30s; regenerate immediately before submitting.

## UI paths

- `/setup`: fields `#username`, `#password`, button "Create owner"; after success the shared key is
  rendered in a `<code>` element with the confirm field `#totpCode` and button "Confirm and enable".
- `/login`: `#username`, `#password`, `#totpCode`, button "Log in". Errors render in `p.error`.
- `/dashboard`: heading "Adesha Dashboard" plus a "Log out" button.
- Session is stored in `localStorage['adesha.session']` (accessToken / accessTokenExpiresAtUtc /
  refreshToken). To force a refresh-token rotation, set `accessTokenExpiresAtUtc` in the past and
  reload; a new access token in localStorage proves the refresh path ran.

## Test-order tips

- Account lockout is 5 failed attempts / 15 minutes: run negative-credential tests last, or log in
  successfully in between (a success resets the counter).
- With the API stopped, the ng dev proxy answers `/api/...` with **502**, so the frontend's
  "status 0 / cannot reach API" branch is not reachable through `ng serve`; expect the generic
  server-error message instead. Test that branch by pointing `ADESHA_API_URL` at a dead port only if
  the proxy is bypassed, or by serving the built app directly.

## Devin Secrets Needed

None — local dev credentials only (`postgres`/`devpass`, a self-chosen JWT signing key).
