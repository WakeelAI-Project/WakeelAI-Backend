# Wakeel AI — Backend

[![Build, Publish and Deploy](https://github.com/WakeelAI-Project/WakeelAI-Backend/actions/workflows/deploy.yaml/badge.svg)](https://github.com/WakeelAI-Project/WakeelAI-Backend/actions/workflows/deploy.yaml)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Database](https://img.shields.io/badge/database-SQL%20Server-CC2927?logo=microsoftsqlserver&logoColor=white)

The ASP.NET Core Web API for **Wakeel AI**, an AI-assisted HR platform. It is the system of record for companies, employees, departments, leave, and generated documents, enforces auth and multi-tenant data isolation, and brokers machine-to-machine access so the AI orchestrator can read employee/company context and act on leave requests and documents on a user's behalf — without ever holding that user's own credentials.

This is one of four repositories that make up the Wakeel AI system:

| Repo | Role |
| --- | --- |
| [WakeelAI-Mobile](https://github.com/WakeelAI-Project/WakeelAI-Mobile) | Employee-facing Flutter app |
| [WakeelAI-Frontend](https://github.com/WakeelAI-Project/WakeelAI-Frontend) | HR/admin web dashboard |
| **WakeelAI-Backend** *(this repo)* | ASP.NET Core API — auth, leave, employees, documents |
| [WakeelAI-AI](https://github.com/WakeelAI-Project/WakeelAI-AI) | Node.js AI orchestrator — chat, RAG over labor law/company policy, leave/document tool-calling |

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Testing](#testing)
- [Project Structure](#project-structure)
- [CI/CD & Deployment](#cicd--deployment)
- [Contributing](#contributing)
- [Team](#team)

## Features

- **Multi-tenant company management** — company registration, profile, HR team invites, and department CRUD, with every query automatically scoped to the caller's company via EF Core global query filters keyed off the JWT's `company_id` claim.
- **JWT authentication** — short-lived access tokens with rotating, HTTP-only-cookie refresh tokens (hashed at rest), BCrypt password hashing, forced password change on first login, and forgot-password via OTP.
- **Role-based access control** — three roles (Company Owner, HR Manager, Employee), enforced on nearly every endpoint.
- **Employee & department management** — CRUD, profile photos, and encryption at rest (AES-256) for sensitive fields like National ID and salary.
- **Leave management** — request/approve/cancel workflow with entitlements, running balances, and medical-attachment uploads.
- **HR document generation** — customizable templates, AI-assisted clause suggestions, PDF rendering ([QuestPDF](https://www.questpdf.com)), and email delivery.
- **Audit log** — a company-wide history of admin actions (Owner-only).
- **AI orchestrator bridge** — an internal API surface, secured by a pre-shared key and identity-forwarding headers, that lets [WakeelAI-AI](https://github.com/WakeelAI-Project/WakeelAI-AI) fetch employee/company context and create, submit, or cancel leave requests and documents on a user's behalf — the AI service never receives a user's JWT.
- **Dashboard metrics** — HR summary stats (employee counts, pending leave, employees on leave today, documents generated).

## Tech Stack

- **[.NET 10](https://dotnet.microsoft.com)** / ASP.NET Core Web API
- **[Entity Framework Core](https://learn.microsoft.com/ef/core)** 10 — SQL Server provider
- **[JWT Bearer authentication](https://learn.microsoft.com/aspnet/core/security/authentication/jwt-authn)** + `BCrypt.Net-Next` for password hashing
- **[QuestPDF](https://www.questpdf.com)** — PDF document generation
- **[UglyToad.PdfPig](https://github.com/UglyToad/PdfPig)** — PDF text extraction (company handbook ingestion)
- **[Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)** — Swagger/OpenAPI docs
- **xUnit**, **Moq**, **FluentAssertions** — unit and integration testing

## Architecture

The solution follows Clean Architecture, with dependencies pointing strictly inward:

```
Wakeel.Domain          # Entities, enums — no dependencies
    ↑
Wakeel.Application     # Interfaces, DTOs, business-logic services — depends on Domain only
    ↑
Wakeel.Infrastructure  # EF Core, repositories, security, external clients — implements Application's interfaces
    ↑
Wakeel.API             # Controllers, middleware, Program.cs — depends on Application + Infrastructure
```

`Wakeel.Tests.Unit` and `Wakeel.Tests.Integration` sit alongside and reference the layers they exercise. Multi-tenancy is enforced by `TenantResolutionMiddleware`, which reads `company_id` from the authenticated JWT and sets it as the ambient tenant for the request; `ApplicationDbContext` then applies global query filters so every tenant-scoped entity is automatically restricted to that company.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB or SQL Express — see [`appsettings.Development.json`](WakeelAI/Wakeel.API/appsettings.Development.json))
- Optionally, a running instance of [WakeelAI-AI](https://github.com/WakeelAI-Project/WakeelAI-AI) for AI-chat and document-generation features to work end to end

### Installation

```bash
git clone https://github.com/WakeelAI-Project/WakeelAI-Backend.git
cd WakeelAI-Backend/WakeelAI
dotnet restore WakeelAI.slnx
dotnet build WakeelAI.slnx
```

Configure the required secrets (see [Configuration](#configuration)) via [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), scoped to `Wakeel.API`:

```bash
cd Wakeel.API
dotnet user-secrets set "Jwt:SecretKey" "<at-least-32-chars>"
dotnet user-secrets set "AiNode:InternalApiKey" "<shared-key>"
dotnet user-secrets set "Encryption:Key" "<base64-encoded-32-byte-key>"
```

Apply migrations and run:

```bash
dotnet ef database update --project ../Wakeel.Infrastructure --startup-project .
dotnet run
```

Swagger UI is served at `/swagger`, and a health check at `/health`.

## Configuration

Configuration lives in `appsettings.json` (see [`appsettings.Example.json`](WakeelAI/Wakeel.API/appsettings.Example.json) for a fully commented template) and is validated at startup — the app fails fast if a required key is missing:

| Key | Purpose | Required |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | SQL Server connection string | yes |
| `Jwt:SecretKey` | JWT signing key (≥ 32 chars) | yes |
| `Jwt:Issuer` / `Jwt:Audience` | JWT issuer/audience | no |
| `Jwt:AccessTokenExpirationMinutes` / `Jwt:RefreshTokenExpirationDays` | Token lifetimes | no |
| `AiNode:BaseUrl` | Base URL of the AI orchestrator | no |
| `AiNode:InternalApiKey` | Shared M2M secret with the AI orchestrator | yes |
| `Encryption:Key` | Base64, 32-byte AES-256 key for encrypted PII fields | yes |
| `Cors:AllowedOrigins` | Allowed frontend origins | no |
| `Smtp:Host` / `Port` / `User` / `Pass` / `From` | Outbound email (falls back to a file-based sender if unset) | no |
| `LeavePolicy:AbandonedDraftRetentionDays` | Leave-draft cleanup window | no |
| `DataRetention:AuditLogRetentionDays` / `GeneratedDocumentRetentionDays` | Retention windows | no |

## Testing

```bash
dotnet test WakeelAI.slnx
```

`Wakeel.Tests.Unit` covers services, controllers, and middleware in isolation with mocks. `Wakeel.Tests.Integration` spins up the full API via `WebApplicationFactory` against a fresh, disposable LocalDB database per test run (created from the current EF model, not migrations), so integration tests never touch a shared or hosted database — including a dedicated suite asserting the multi-tenant query filters actually isolate companies from each other.

## Project Structure

```
WakeelAI/
├── Wakeel.Domain/              # Entities, enums
├── Wakeel.Application/         # Interfaces, DTOs, business-logic services
├── Wakeel.Infrastructure/      # EF Core (DbContext, migrations), JWT/refresh-token/encryption
│                                #   implementations, external HTTP clients
├── Wakeel.API/                 # Controllers, middleware, Program.cs, appsettings
├── Wakeel.Tests.Unit/          # Mocked unit tests
├── Wakeel.Tests.Integration/   # WebApplicationFactory-based integration tests
└── WakeelAI.slnx               # Solution file
```

## CI/CD & Deployment

**[`deploy.yaml`](.github/workflows/deploy.yaml)** runs on every push to `develop`: restores and builds the solution, runs the full test suite, publishes `Wakeel.API`, applies EF Core migrations against the target database (`dotnet ef database update`), and deploys to **[MonsterASP.NET](https://www.monsterasp.net)**.

- Production API: [wakeel-ai-api.runasp.net](https://wakeel-ai-api.runasp.net) (Swagger at `/swagger`, health check at `/health`)

## Contributing

Branch off `develop` (not `main`) and name branches by what they do: `feature/<name>` for new functionality, `fix/<name>` for bug fixes, `docs/<name>` for documentation, `chore/<name>` for maintenance. Open a PR into `develop`; `main` is only updated by merging a ready `develop` for release.

## Team

Built by the Wakeel AI graduation team as an ITI AI Capstone project:

- [Assem Mohamed](https://github.com/Assem-Mohamed)
- [Mohanad Tarek](https://github.com/HONDA-74)
- [Hosam Abdullah](https://github.com/Hosam-Abdullah)
- [Abdelrahman Ahmed Yasser](https://github.com/0Abdelrahman1)
- [Abdelrahman Diab](https://github.com/Diab63)
