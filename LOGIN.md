# Adesha Login Guide

This guide covers the three login flows in Adesha:

1. **Owner setup** — creates the single application account (TOTP optional).
2. **App login** — authenticates into Adesha with a password, optionally plus TOTP.
3. **Broker login (m.Stock)** — links an m.Stock account to Adesha.

---

## 1. Owner setup

The first time Adesha runs there is no user account. Visit the setup page:

```
http://<adesha-web>/setup
```

1. Enter an **Owner username**.
2. Enter a **Password** of at least **12 characters**.
3. Click **Create owner**.

The account is usable immediately for password-only login. A TOTP secret and
`otpauth://` link are also shown; TOTP is **optional**:

- To enable the second factor, scan the secret in an authenticator app, enter the
  6-digit code, and click **Confirm and enable**.
- To skip TOTP, click **Skip — password only**. You can log in with just the password.

> The backend enforces `SetupOwnerRequestValidator`: 12–256 character password.
> TOTP confirmation (`/api/auth/setup/confirm-totp`) is optional.

---

## 2. App login

The login page offers a **method selector**: *Password only* or *Password + TOTP*.
Pick the one that matches the account's configuration.

`POST /api/auth/login`

```json
{
  "username": "owner",
  "password": "..."
}
```

or, when TOTP is enabled on the account:

```json
{
  "username": "owner",
  "password": "...",
  "totpCode": "123456"
}
```

The backend decides whether a TOTP code is required based on the account's
`TwoFactorEnabled` flag: if TOTP is enabled, a valid `totpCode` is mandatory
(failed attempts count toward lockout); if TOTP is not enabled, the password
alone authenticates the owner and `totpCode` is ignored.

On success, the response is an `accessToken` / `refreshToken` pair. The access token is short-lived (10 minutes by default); use `/api/auth/refresh` with the refresh token when it expires.

---

## 3. Broker login (m.Stock)

Adesha can talk to m.Stock through either the **Type A** or **Type B** API surface. Both are 3-step flows: login, then OTP/TOTP, then session.

### 3.1 Select the API type

Set `MStock:ApiType` in configuration:

```json
{
  "MStock": {
    "BaseUrl": "https://api.mstock.trade",
    "ApiType": "TypeA"
  }
}
```

Valid values: `TypeA` or `TypeB`. Default is `TypeA`.

### 3.2 Differences between Type A and Type B

| | Type A | Type B |
|---|---|---|
| Endpoint path | `/openapi/typea/...` | `/openapi/typeb/...` |
| Request body encoding | `application/x-www-form-urlencoded` | `application/json` |
| Login body | `username` + `password` | `clientcode` + `password` + `totp` + `state` |
| Session OTP body | `api_key` + `request_token` (OTP) + `checksum=L` | `refreshToken` (from login) + `otp` |
| Session TOTP body | `api_key` + `totp` | `refreshToken` (from login) + `totp` |
| Session auth header | `Authorization: token {api_key}:{jwtToken}` | `Authorization: Bearer {jwtToken}` + `X-PrivateKey: {api_key}` |

### 3.3 UI flow

After logging into Adesha, go to **Dashboard → Broker login (m.Stock)** (`/broker-login`).
The UI walks through the same two-step flow: enter credentials, choose OTP or TOTP,
and complete the session.

### 3.4 HTTP API

The `IBrokerAdapter` methods are exposed through authenticated HTTP endpoints:

1. `POST /api/broker/login/initiate`
   ```json
   { "brokerId": "MStock", "username": "...", "password": "..." }
   ```
   - Type A: triggers an OTP to the registered mobile.
   - Type B: returns a refresh handle that is stored temporarily in Redis for the next step.

2. `POST /api/broker/login/complete-otp`
   ```json
   { "brokerId": "MStock", "otp": "123456" }
   ```
   - Exchanges the OTP for a broker session.

   **OR**

   `POST /api/broker/login/complete-totp`
   ```json
   { "brokerId": "MStock", "totp": "123456" }
   ```
   - Use this when TOTP is enabled on the m.Stock account (no SMS OTP).

3. `GET /api/broker/session?brokerId=MStock` — current session status.

4. `POST /api/broker/logout` — invalidates the session at the broker and clears it locally.

### 3.5 Session lifetime

m.Stock sessions are valid for the shorter of **12 hours** or until midnight IST. Adesha uses the conservative 12-hour window. After expiry, the broker login flow must be repeated.

### 3.5 API key

`MStock:ApiKey` must be provided via configuration or secrets. The key is sent as `X-PrivateKey` (Type B) or in the `Authorization` header (Type A) and should never be exposed in client code.

---

## See also

- MStock Type A API docs: https://tradingapi.mstock.com/docs/v1/typeA/User/
- MStock Type B API docs: https://tradingapi.mstock.com/docs/v1/typeB/User/
