# Finkletex

A locked-down CLI for regular employees to manage their own timesheets in [Tripletex](https://www.tripletex.no/), authenticated via Google OAuth.

Unlike the [admin CLI](https://github.com/murdahl/tripletex-sdk), this tool restricts users to their own data — no access to other employees' timesheets, no delete/approve operations, and no manual token configuration. Authentication is handled through a Google OAuth flow restricted to `@fink.no` accounts.

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Authentication](#authentication)
- [Commands](#commands)
  - [login](#login)
  - [timesheet](#timesheet)
  - [project](#project)
  - [activity](#activity)
  - [config](#config)
- [Configuration](#configuration)
- [Server (Auth Function)](#server-auth-function)
  - [Architecture](#architecture)
  - [Endpoints](#endpoints)
  - [User Management](#user-management)
  - [Environment Variables](#environment-variables)
  - [Deployment](#deployment)
- [Development](#development)
  - [Prerequisites](#prerequisites)
  - [Building the CLI](#building-the-cli)
  - [Building the Server](#building-the-server)
  - [Project Structure](#project-structure)
- [Release](#release)
- [Security](#security)
- [Comparison with Admin CLI](#comparison-with-admin-cli)

## Features

- **Google OAuth login** — no manual token handling, restricted to `@fink.no` domain
- **Employee-scoped** — all operations are locked to the authenticated user's employee ID
- **Timesheet management** — log hours, log full weeks, list entries, view recent activity
- **Project browsing** — list and search projects (read-only)
- **Activity selection** — interactively pick and save default activities
- **Interactive wizard** — guided prompts for logging hours with project/activity selection
- **Multiple output formats** — human-readable tables or `--json` for scripting
- **Cross-platform** — macOS (ARM/x64), Linux (x64/ARM64), Windows (x64)

## Installation

### From GitHub Releases

Download the latest binary for your platform from [Releases](../../releases):

| Platform              | File                                    |
| --------------------- | --------------------------------------- |
| macOS (Apple Silicon) | `tripletex-employee-osx-arm64.tar.gz`   |
| macOS (Intel)         | `tripletex-employee-osx-x64.tar.gz`     |
| Linux (x64)           | `tripletex-employee-linux-x64.tar.gz`   |
| Linux (ARM64)         | `tripletex-employee-linux-arm64.tar.gz` |
| Windows (x64)         | `tripletex-employee-win-x64.zip`        |

```bash
# macOS / Linux
tar -xzf tripletex-employee-osx-arm64.tar.gz
sudo mv finkletex /usr/local/bin/

# Windows — extract the zip and add to PATH
```

### From Source

```bash
dotnet publish src/Tripletex.EmployeeCli/Tripletex.EmployeeCli.csproj \
  -c Release -o ./publish
```

## Authentication

The CLI uses a Google OAuth flow to authenticate users. No tokens are configured manually.

```
finkletex login
```

**What happens:**

1. The CLI starts a temporary local HTTP server
2. Your browser opens to the auth function, which redirects to Google
3. You sign in with your `@fink.no` Google account
4. The auth function validates your email, looks up your Tripletex employee ID, and creates a signed token bundle
5. The token is sent back to the local server and saved to `~/.tripletex-employee/config.json`
6. You're ready to use the CLI

```
$ finkletex login
Opening browser for authentication...
Logged in as ole@fink.no (Employee ID: 123)
```

**Requirements:**

- A `@fink.no` Google account
- Your email must be registered in the auth function's `USERS_JSON` environment variable
- Browser access (the CLI will attempt to open your default browser)

If the browser doesn't open automatically, the CLI prints the URL for you to visit manually.

## Commands

All commands (except `login` and `config show`) require you to be logged in first.

### login

```bash
finkletex login
```

Authenticate via Google OAuth. Opens your browser and waits for the callback. Times out after 120 seconds.

The auth URL can be overridden with the `TRIPLETEX_AUTH_URL` environment variable.

### timesheet

#### `timesheet log [hours]`

Log hours for a single day. Runs an interactive wizard if arguments are omitted.

```bash
# Interactive — prompts for project, activity, hours, date, comment
finkletex timesheet log

# Direct — log 7.5 hours today
finkletex timesheet log 7.5

# Full specification
finkletex timesheet log 7.5 --date 2026-03-06 --comment "Feature work" \
  --project-id 100 --activity-id 200
```

**Options:**
| Option | Description |
|--------|-------------|
| `hours` | Number of hours (optional, prompted if omitted) |
| `--date` | Date in `yyyy-MM-dd` format (default: today) |
| `--comment` | Comment for the entry |
| `--project-id` | Project ID (uses saved default if omitted) |
| `--activity-id` | Activity ID (uses saved default if omitted) |

**Interactive wizard features:**

- Filterable project and activity selection (type to search when list is long)
- Back navigation between steps
- Option to save selections as defaults
- Conflict handling — if hours are already logged for that day/project/activity, offers to overwrite

#### `timesheet log-week <week-start> <mon> <tue> <wed> <thu> <fri>`

Log hours for an entire week in one command.

```bash
# Standard work week
finkletex timesheet log-week 2026-03-02 7.5 7.5 7.5 7.5 7.5

# With weekend and comment
finkletex timesheet log-week 2026-03-02 7.5 7.5 7.5 7.5 7.5 \
  --sat 4 --comment "Sprint 12"

# Override project/activity
finkletex timesheet log-week 2026-03-02 7.5 7.5 7.5 7.5 7.5 \
  --project-id 100 --activity-id 200
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `week-start` | Monday of the week (`yyyy-MM-dd`) |
| `mon` - `fri` | Hours for each weekday |

**Options:**
| Option | Description |
|--------|-------------|
| `--sat` | Hours for Saturday (default: 0) |
| `--sun` | Hours for Sunday (default: 0) |
| `--comment` | Comment for all entries |
| `--project-id` | Project ID (uses saved default) |
| `--activity-id` | Activity ID (uses saved default) |

#### `timesheet list`

List your timesheet entries with optional date and project filters.

```bash
# All your entries
finkletex timesheet list

# Filter by date range
finkletex timesheet list --from-date 2026-03-01 --to-date 2026-03-31

# Filter by project
finkletex timesheet list --project-id 100

# JSON output for scripting
finkletex timesheet list --json
```

**Options:**
| Option | Description |
|--------|-------------|
| `--from-date` | Start date filter (`yyyy-MM-dd`) |
| `--to-date` | End date filter (`yyyy-MM-dd`) |
| `--project-id` | Filter by project ID |

#### `timesheet recent`

Show your timesheet entries from the last 30 days, newest first.

```bash
finkletex timesheet recent
finkletex timesheet recent --json
```

### project

Read-only project browsing.

#### `project list`

```bash
finkletex project list
finkletex project list --json
```

#### `project search`

```bash
finkletex project search --name "Website"
```

### activity

#### `activity select`

Interactively select and save a default activity for a project.

```bash
# Uses default project
finkletex activity select

# Specify project
finkletex activity select --project-id 100
```

### config

#### `config show`

Display current configuration (tokens are masked).

```bash
$ finkletex config show
┌─────────────────┬──────────────────────────────┐
│ Setting         │ Value                        │
├─────────────────┼──────────────────────────────┤
│ Email           │ ole@fink.no                  │
│ Employee Name   │ Ole Magnus                   │
│ Employee ID     │ 123                          │
│ Consumer Token  │ abc1********xyz9             │
│ Employee Token  │ def2********uvw8             │
│ Environment     │ production                   │
│ Default Project │ Website Redesign (ID: 100)   │
│ Default Activity│ Development (ID: 200)        │
└─────────────────┴──────────────────────────────┘
```

### Global Options

| Option      | Description                              |
| ----------- | ---------------------------------------- |
| `--json`    | Output results as JSON instead of tables |
| `--help`    | Show help for any command                |
| `--version` | Show version                             |

## Configuration

Configuration is stored at `~/.tripletex-employee/config.json` (separate from the admin CLI's `~/.tripletex/`).

```json
{
  "consumerToken": "...",
  "employeeToken": "...",
  "employeeId": 123,
  "employeeName": "Ole Magnus",
  "email": "ole@fink.no",
  "environment": "production",
  "defaultProjectId": 100,
  "defaultProjectName": "Website Redesign",
  "defaultActivityId": 200,
  "defaultActivityName": "Development"
}
```

**Tokens are set exclusively through `finkletex login`** — there is no `config set` command. Default project and activity are saved interactively when using the timesheet wizard or `activity select`.

**Environment variable overrides:**
| Variable | Description |
|----------|-------------|
| `FINKLETEX_AUTH_URL` | Override the default auth function URL |
| `TRIPLETEX_CONSUMER_TOKEN` | Override consumer token from config |
| `TRIPLETEX_EMPLOYEE_TOKEN` | Override employee token from config |

## Server (Auth Function)

The `server/` directory contains a TypeScript serverless function deployed to Scaleway that handles the Google OAuth flow and maps Google accounts to Tripletex employee IDs.

### Architecture

```
Browser                    Scaleway Function              Google OAuth
  │                              │                            │
  │  GET /auth?port=X&state=Y    │                            │
  │─────────────────────────────>│                            │
  │  302 → Google OAuth          │                            │
  │<─────────────────────────────│                            │
  │                              │                            │
  │  User authenticates          │                            │
  │─────────────────────────────────────────────────────────>│
  │                              │   GET /callback?code=Z     │
  │                              │<───────────────────────────│
  │                              │                            │
  │                              │  Validate domain (@fink.no)│
  │                              │  Look up USERS_JSON env        │
  │                              │  Sign payload with HMAC    │
  │                              │                            │
  │  302 → localhost:X/callback?payload=SIGNED&state=Y        │
  │<─────────────────────────────│                            │
  │                              │                            │
  │  CLI receives tokens + employeeId                         │
  │  Saves to ~/.tripletex-employee/config.json               │
```

### Endpoints

| Endpoint    | Method | Description                                                                             |
| ----------- | ------ | --------------------------------------------------------------------------------------- |
| `/auth`     | GET    | Initiates OAuth flow. Params: `port`, `state`                                           |
| `/callback` | GET    | Google OAuth callback. Validates domain, maps email to employee, returns signed payload |
| `/health`   | GET    | Health check, returns `{"status": "ok"}`                                                |

### User Management

Employee mappings are provided via the `USERS_JSON` environment variable (set as a Scaleway secret). See `server/users.json.example` for the format:

```json
{
  "ole@fink.no": { "employeeId": 123, "name": "Ole Magnus" },
  "someone@fink.no": { "employeeId": 456, "name": "Someone Else" }
}
```

**To add a new employee:**

1. Update the `USERS_JSON` secret in the Scaleway console
2. The function picks up changes on next invocation (no redeploy needed)

Users with valid `@fink.no` Google accounts but not in `USERS_JSON` will see a clear error message asking them to contact their administrator.

### Environment Variables

Configure these as Scaleway secrets:

| Variable                   | Required | Description                                   |
| -------------------------- | -------- | --------------------------------------------- |
| `GOOGLE_CLIENT_ID`         | Yes      | Google OAuth 2.0 client ID                    |
| `GOOGLE_CLIENT_SECRET`     | Yes      | Google OAuth 2.0 client secret                |
| `TRIPLETEX_CONSUMER_TOKEN` | Yes      | Shared Tripletex consumer token               |
| `TRIPLETEX_EMPLOYEE_TOKEN` | Yes      | Shared Tripletex employee token               |
| `HMAC_SECRET`              | Yes      | Secret key for signing payloads               |
| `FUNCTION_URL`             | Yes      | Public URL of the deployed function           |
| `USERS_JSON`               | Yes      | JSON mapping of emails to employee IDs        |
| `ALLOWED_DOMAIN`           | No       | Email domain restriction (default: `fink.no`) |

### Deployment

```bash
cd server
npm install
npm run build

# Deploy to Scaleway (using their CLI or console)
# Entry point: dist/handler.handle
# Runtime: Node.js 22
```

**Google OAuth Setup:**

1. Go to [Google Cloud Console](https://console.cloud.google.com/) → APIs & Services → Credentials
2. Create an OAuth 2.0 Client ID (Web application)
3. Add `{FUNCTION_URL}/callback` as an authorized redirect URI
4. Copy the client ID and secret to the function's environment variables

## Development

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 22+](https://nodejs.org/) (for the server function)

### Building the CLI

```bash
# Restore and build
dotnet build src/Tripletex.EmployeeCli/Tripletex.EmployeeCli.csproj

# Run directly
dotnet run --project src/Tripletex.EmployeeCli -- timesheet recent

# Publish self-contained binary
dotnet publish src/Tripletex.EmployeeCli/Tripletex.EmployeeCli.csproj \
  -c Release -r osx-arm64 -o ./publish
```

### Building the Server

```bash
cd server
npm install
npm run build     # Compiles TypeScript to dist/
npm run dev       # Watch mode
```

### Project Structure

```
tripletex-employee-cli/
├── server/                              # TypeScript serverless function (Scaleway)
│   ├── handler.ts                       # Auth endpoints (/auth, /callback, /health)
│   ├── users.json.example               # Example email → employee ID mapping
│   ├── package.json
│   └── tsconfig.json
├── src/
│   └── Tripletex.EmployeeCli/           # C# CLI application
│       ├── Program.cs                   # Entry point, command registration
│       ├── ClientFactory.cs             # TripletexClient factory
│       ├── OutputFormatter.cs           # Table/JSON output formatting
│       ├── Configuration/
│       │   ├── CliConfig.cs             # Config model (tokens, employee info, defaults)
│       │   └── ConfigStore.cs           # Persistence (~/.tripletex-employee/)
│       └── Commands/
│           ├── LoginCommand.cs          # Google OAuth flow with localhost callback
│           ├── TimesheetCommand.cs      # log, log-week, list, recent
│           ├── ProjectCommand.cs        # list, search
│           ├── ActivityCommand.cs       # select
│           └── ConfigCommand.cs         # show
├── .github/workflows/
│   └── release.yml                      # Multi-platform build + GitHub Release
└── .gitignore
```

**Dependencies:**

- [`Tripletex.Api`](https://www.nuget.org/packages/Tripletex.Api) — NuGet package for all Tripletex API operations
- [`System.CommandLine`](https://github.com/dotnet/command-line-api) — CLI argument parsing
- [`Spectre.Console`](https://spectreconsole.net/) — Rich terminal output (tables, prompts, colors)

## Release

Releases are automated via GitHub Actions. To create a release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

This triggers the release workflow which:

1. Builds self-contained binaries for 5 platforms (macOS ARM/x64, Linux x64/ARM64, Windows x64)
2. Archives them (`.tar.gz` for Unix, `.zip` for Windows)
3. Creates a GitHub Release with auto-generated release notes

## Security

**Authentication:**

- Google OAuth with domain restriction (`hd=fink.no`) — only `@fink.no` accounts can authenticate
- Double validation: Google's `hd` parameter + server-side domain check on the verified email
- Email must exist in `USERS_JSON` — valid Google accounts without a mapping are rejected
- HMAC-signed payloads prevent tampering during the localhost redirect
- OAuth state parameter prevents CSRF attacks

**Authorization:**

- Employee ID is set during login and cannot be changed by the user
- All timesheet queries are forced to the authenticated employee's ID
- No delete, approve, or admin operations are exposed
- Project and activity commands are read-only

**Token storage:**

- Tokens are stored in plaintext at `~/.tripletex-employee/config.json`
- This is consistent with standard CLI tools (AWS CLI, gcloud, GitHub CLI, kubectl)
- Protected by filesystem permissions — only the owning user can read their home directory
- Tokens can be overridden via environment variables for CI/CD use cases

**Server-side:**

- Tripletex consumer/employee tokens are stored as Scaleway secrets, never exposed to the client
- The HMAC secret is server-side only — clients cannot forge payloads
- Token payloads include an expiration timestamp

## Comparison with Admin CLI

| Feature               | Admin CLI                          | Employee CLI             |
| --------------------- | ---------------------------------- | ------------------------ |
| Command               | `tripletex`                        | `finkletex`              |
| Authentication        | Manual token config                | Google OAuth             |
| Token setup           | `config set --consumer-token`      | `login` (automatic)      |
| Employee scope        | Any employee (via `--employee-id`) | Own employee only        |
| Timesheet log         | All employees                      | Own only                 |
| Timesheet list        | All employees                      | Own only                 |
| Timesheet delete      | Yes                                | No                       |
| Timesheet approve     | Yes                                | No                       |
| Timesheet total-hours | Yes                                | No                       |
| Employee management   | Yes                                | No                       |
| Customer management   | Yes                                | No                       |
| Invoice management    | Yes                                | No                       |
| Supplier management   | Yes                                | No                       |
| Project write ops     | Yes                                | No (read-only)           |
| Config directory      | `~/.tripletex/`                    | `~/.tripletex-employee/` |
| Distribution          | NuGet + binaries                   | Binaries only            |
