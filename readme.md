# Remote Assistant

A multi-project .NET 10 and Angular 18 application with Google OAuth login and Telegram bot management. Users register to individual bots via Telegram to trigger automated jobs.

---

## Architecture

```mermaid
sequenceDiagram
    actor Admin
    actor User as Telegram User
    participant Angular as Angular UI
    participant WebApi as .NET Web API
    participant Worker as Telegram Bot Service
    participant DB as SQL Server Express
    participant Google as Google OAuth

    Admin->>Angular: Open app → /login
    Admin->>Angular: Click "Sign in with Google"
    Angular->>WebApi: GET /api/admin/auth/google-login
    WebApi->>Angular: 302 Redirect to Google (openid email profile)
    Angular->>Google: Follow Redirect
    Google-->>Admin: Prompt for Consent
    Admin->>Google: Approve
    Google-->>WebApi: Redirect to /api/admin/auth/callback?code=xxx
    WebApi->>Google: Exchange Code for Tokens
    Google-->>WebApi: ID Token
    WebApi->>DB: Save Admin Email
    WebApi->>WebApi: Validate ID Token, Issue JWT
    WebApi->>WebApi: Set JWT as Cookie
    WebApi-->>Angular: 302 Redirect to /bots
    Angular->>Angular: Read JWT from Cookie → localStorage

    Admin->>Angular: Add/manage bots, view registrations

    User->>Worker: Send /register
    Worker->>DB: Insert BotRegistration (IsActive=true)
    Worker-->>User: "You are now registered to this bot."

    User->>Worker: Send /unregister
    Worker->>DB: Update BotRegistration (IsActive=false)
    Worker-->>User: "You have been unregistered."
```

---

## Solution Structure

| Project | Description |
|---------|-------------|
| `RemoteAssistant.Core` | Shared models and EF Core DbContext |
| `RemoteAssistant.WebApi` | REST API: auth, OAuth callbacks, bot/registration management |
| `RemoteAssistant.Worker` | Background service: Telegram bot polling, per-bot `/register` / `/unregister` |
| `remote-assistant-admin-ui` | Angular 18 SPA — glassmorphic dark theme |

---

## Frontend Routes

| Path | Auth | Description |
|------|------|-------------|
| `/login` | No | Google sign-in page, first-run credential setup |
| `/bots` | Yes | Main page: bot list, add/edit/delete, expand registrations |
| `/` | - | Redirects to `/bots` (or `/login` if unauthenticated) |

---

## Database Schema

### `TelegramBots` Table

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | `int` | NOT NULL (PK) | Auto-increment |
| `Name` | `nvarchar(100)` | NOT NULL | Bot display name |
| `Description` | `nvarchar(500)` | NULL | Optional description |
| `Token` | `nvarchar(500)` | NOT NULL | Bot token from @BotFather |
| `IsActive` | `bit` | NOT NULL | Whether worker polls this bot |
| `CreatedAt` | `datetime2` | NOT NULL | - |
| `UpdatedAt` | `datetime2` | NOT NULL | - |

### `BotRegistrations` Table

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | `int` | NOT NULL (PK) | Auto-increment |
| `TelegramId` | `bigint` | NOT NULL | Telegram user ID |
| `BotId` | `int` | NOT NULL (FK) | Which bot they registered to |
| `IsActive` | `bit` | NOT NULL | Active or unregistered |
| `RegisteredAt` | `datetime2` | NOT NULL | - |
| `UnregisteredAt` | `datetime2` | NULL | When they unregistered |

> Unique constraint on `(TelegramId, BotId)` — same Telegram user can register to multiple bots independently.

### `OAuthProviders` Table

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Provider` | `nvarchar(50)` | NOT NULL (PK) | Provider name (e.g. `Google`) |
| `ClientId` | `nvarchar(500)` | NULL | OAuth Client ID |
| `ClientSecret` | `nvarchar(500)` | NULL | OAuth Client Secret |
| `UpdatedAt` | `datetime2` | NOT NULL | Last modified |

> Designed for multi-provider support — add rows for GitHub, Microsoft, etc.

### `SystemSettings` Table

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Key` | `nvarchar(100)` | NOT NULL (PK) | Setting key |
| `Value` | `nvarchar(max)` | NULL | Setting value |
| `UpdatedAt` | `datetime2` | NOT NULL | Last modified |

> Stores `GoogleRefreshToken` and `GoogleAdminEmail`. OAuth credentials and bot tokens have their own dedicated tables.

---

## Prerequisites

- **.NET 10 SDK**
- **Node.js** v18+ & npm
- **SQL Server Express** (local)
- **Google Cloud Console** project with OAuth 2.0 credentials

---

## Google Cloud Console Setup

1. Go to the [Google Cloud Console](https://console.cloud.google.com/)
2. Create a project → **APIs & Services > Credentials**
3. Configure the **OAuth Consent Screen** (External) with scopes: `openid`, `email`, `profile`
4. Create **OAuth Client ID** → Web Application
5. Under **Authorized redirect URIs**, add:
   ```
   http://localhost:5000/api/admin/auth/callback
   ```
6. Save to get your **Client ID** and **Client Secret**

---

## Telegram Bot Setup

1. Open Telegram → search **@BotFather**
2. Send `/newbot` and follow prompts
3. Save the HTTP API **Bot Token**

---

## Running the Application

### 1. Start the Web API
```bash
dotnet run --project RemoteAssistant.WebApi
```
Starts on `http://localhost:5000`. Creates database tables on first startup.

### 2. Start the Worker Service
```bash
dotnet run --project RemoteAssistant.Worker
```
Polls the first active bot from the `TelegramBots` table. Re-checks for new/updated tokens every 15 seconds.

### 3. Start the Angular UI
```bash
cd remote-assistant-admin-ui
npm install
npm run start
```
Starts on `http://localhost:4200`.

---

## Configuration Workflow

> **First run**: Configure your Google Client ID and Client Secret in `appsettings.json` (under the `"Google"` section) or via the login page form. Without these, OAuth will not work.

1. Open **`http://localhost:4200`** — you'll land on the **login page**
2. If credentials are not yet configured, enter your **Client ID** and **Client Secret** and click **Save Credentials**
3. Click **Sign in with Google** — the server redirects to Google for authentication
4. After login, you're taken to the **bots page** where you can manage Telegram bots
5. Click **+ Add Bot**, enter a **name**, optional **description**, and the **Bot Token** from @BotFather
6. Click any bot to expand its registered users

> Optional: Restrict login to a specific Google account by setting `"Admin:AllowedEmail": "admin@example.com"` in `appsettings.json`.

---

## Authentication

Google OAuth is the single entry point. Sign-in requests `openid email profile` scopes.

**Flow:**
1. User clicks "Sign in with Google" on `/login`
2. Angular redirects to `GET /api/admin/auth/google-login`
3. Server responds with 302 redirect to Google
4. User consents — Google redirects to `GET /api/admin/auth/callback?code=xxx`
5. Server exchanges code for tokens, saves admin email to DB
6. Server issues a JWT, sets it as a non-httpOnly cookie (`auth_token`) and redirects to `/bots`
7. Angular `AuthService` reads cookie on init, stores JWT in `localStorage`, clears cookie
8. `AuthInterceptor` attaches JWT as `Authorization: Bearer` header on all API requests
9. `AuthGuard` protects the `/bots` route, redirecting to `/login` if no valid JWT

**First-run setup:** If credentials aren't configured, the login page shows a form to enter Client ID and Client Secret. These are saved to the database via `POST /api/admin/config/google` (anonymous).

---

## User Registration Flow (Telegram)

Each bot is an independent domain — users register to each bot separately.

1. Open Telegram → find your bot → send `/start`
2. Send `/register` — registers you to that specific bot
3. Send `/unregister` — unregisters you from that bot
4. Re-sending `/register` after `/unregister` re-activates your registration
5. View registrations on the bots page (click any bot to expand its user list)

---

## API Endpoints

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/api/admin/auth/google-login` | No | Redirect to Google OAuth |
| `GET` | `/api/admin/auth/callback` | No | Google OAuth callback — exchanges code, issues JWT cookie |
| `GET` | `/api/admin/auth/status` | Yes | Current authenticated user email |
| `POST` | `/api/admin/auth/logout` | No | Logout (client-side) |
| `GET` | `/api/admin/config` | No | Configuration status (booleans + bot count) |
| `POST` | `/api/admin/config/google` | No | Save Google OAuth credentials |
| `GET` | `/api/admin/bots` | Yes | List all Telegram bots |
| `POST` | `/api/admin/bots` | Yes | Create a bot |
| `PUT` | `/api/admin/bots/{id}` | Yes | Update a bot |
| `PATCH` | `/api/admin/bots/{id}/toggle` | Yes | Enable/disable a bot |
| `DELETE` | `/api/admin/bots/{id}` | Yes | Delete a bot |
| `GET` | `/api/admin/bots/{id}/registrations` | Yes | List registrations for a bot |

---

## Environment Files

`src/environments/environment.ts` — template (tracked in git):
```ts
export const environment = {
  production: true,
  apiBaseUrl: 'http://localhost:5000/api/admin'
};
```

`src/environments/environment.development.ts` — development (gitignored). Copy the template and adjust as needed.

---

## Configuration Keys (appsettings.json)

```jsonc
{
  "Google": {
    "ClientId": "",             // Your Google OAuth Client ID
    "ClientSecret": ""          // Your Google OAuth Client Secret
  },
  "Frontend": {
    "BaseUrl": "http://localhost:4200"  // Where to redirect after OAuth
  },
  "Admin": {
    "AllowedEmail": ""          // Optional: restrict login to one email
  },
  "Jwt": {
    "Key": "",                  // Custom signing key (min 32 chars, auto-generated if missing)
    "Issuer": "RemoteAssistant",
    "Audience": "RemoteAssistant-AdminUI"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=SchedulerTelegramDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

> Google OAuth credentials can be set in `appsettings.json` (fallback) **or** via the login page UI form (saved to the `OAuthProviders` database table keyed by `Provider = "Google"`). The server checks the database first, then falls back to config.
