# Build Summary

## What Was Built

A complete Building Maintenance Management System MVP with HOA/Va'ad fee management:

### Backend (ASP.NET Core .NET 10 Preview)
- **21 domain entities** with EF Core code-first, audit fields, and soft-delete
- **13 API controllers** with 60+ REST endpoints covering all modules
- **JWT authentication** with ASP.NET Core Identity, token refresh flow
- **Role-based authorization** (Admin, Manager, Tenant, Vendor) with row-level access
- **File storage abstraction** (Local + Azure Blob Storage implementations)
- **Background job service** with idempotent generators using JobRunLog
- **Recurring payment job** with retry logic (max 3), email notifications, per-building gateway resolution
- **Multi-provider payment gateway** (Fake/Meshulam/Pelecard/Tranzila) with `IPaymentGateway` + `PaymentGatewayFactory`
- **HOA fee service** with BySqm, FixedPerUnit, and ManualPerUnit calculation methods
- **Financial ledger** tracking all charges, payments, and adjustments
- **Email notification abstraction** (Logging + SMTP-ready)
- **Data seeding** for development with 4 demo users, buildings, vendors, assets
- **Swagger/OpenAPI** documentation
- **SQLite** for dev, SQL Server/Azure SQL ready

### Frontend (React + TypeScript + Vite + MUI)
- **Login page** with JWT token management (in-memory + refresh)
- **Tenant portal**: Create service requests with photo upload, view my requests
- **Manager portal**: Dashboard, Buildings CRUD, Vendors CRUD, Assets & Preventive Plans, Service Requests triage, Work Orders management, Cleaning Plans, Job generators, **HOA fee plans, charge generation, collection & aging reports with CSV export, Payment Provider Configuration page**
- **Tenant portal**: **My Charges page with outstanding balance, hosted payment flow (Pay Online → redirect to provider), token-based pay, payment method management (add/remove/set default), payment history, payment success/cancel pages**
- **Vendor portal**: View assigned work orders, update status, add notes, upload photos
- **Role-based navigation** and route protection
- **Responsive layout** (mobile-friendly with drawer navigation)
- **Timezone handling** (UTC storage, Asia/Jerusalem display)
- **File upload with client-side validation** (type, size, preview)

### DevOps & Infrastructure
- **Dockerfiles** for backend and frontend
- **docker-compose.yml** with override for local development
- **GitHub Actions workflow** for CI/CD to Azure
- **Azure deployment guide** with az CLI commands

## SDK & Fallback Note

- **Used: .NET 10 Preview** (SDK 10.0.200-preview.0.26103.119)
- EF Core packages: version 9.x (compatible with net10.0 target)
- The project targets `net10.0` and builds successfully with the preview SDK
- If .NET 10 preview SDK is unavailable, change `<TargetFramework>` to `net9.0` in all `.csproj` files

## How to Run Locally

### Backend
```bash
cd src/BuildingManagement.Api
dotnet run
# API: http://localhost:5219
# Swagger: http://localhost:5219/swagger
```

### Frontend
```bash
cd frontend
npm install
npm run dev
# UI: http://localhost:5173
```

### Docker
```bash
docker-compose up --build
# Backend: http://localhost:5219
# Frontend: http://localhost:3000
```

## Demo Credentials (Dev Only)

| User | Password | Role |
|------|----------|------|
| admin@example.com | Demo@123! | Admin |
| manager@example.com | Demo@123! | Manager |
| tenant@example.com | Demo@123! | Tenant |
| vendor@example.com | Demo@123! | Vendor |

## Finance Module Summary

| Feature | Status |
|---------|--------|
| HOA Fee Plans (BySqm / Fixed / Manual) | Done |
| Monthly Charge Generation (idempotent) | Done |
| Hosted Payment Page (redirect to provider) | Done |
| Tokenized Payment Methods (hosted flow) | Done |
| Pay Outstanding Charges (token + hosted) | Done |
| Charge Adjustment by Manager | Done |
| Collection Status Report + CSV | Done |
| Aging Report + CSV | Done |
| Financial Ledger (Charge/Payment/Adjustment) | Done |
| Recurring Payment Background Job | Done |
| Payment Webhook Endpoint | Done |
| Fake Payment Gateway (full dev simulation) | Done |
| Meshulam Gateway (skeleton + TODO) | Structure ready |
| Pelecard Gateway (skeleton + TODO) | Structure ready |
| Tranzila Gateway (skeleton + TODO) | Structure ready |
| PaymentGatewayFactory (per-building routing) | Done |
| PaymentProviderConfig entity + CRUD API | Done |
| Provider Config UI (Manager) | Done |
| WebhookEventLog (idempotent webhooks) | Done |
| Webhook per-provider routing | Done |
| Payment success/cancel pages (redirect flow) | Done |
| Health check endpoint (/health) | Done |
| RBAC: Vendor blocked from finance | Done |

## What Remains for V2

- [ ] Full i18n support (Hebrew RTL)
- [ ] SMTP email integration (currently logging only)
- [ ] Database-backed refresh token store
- [ ] Push notifications (SignalR/WebSocket)
- [ ] Multi-company / multi-tenant support
- [ ] Complete Meshulam/Pelecard/Tranzila gateway implementations (fill in TODO sections with real API calls)
- [ ] Invoice/receipt PDF generation
- [ ] Automatic overdue status update job
- [ ] Partial payment allocation UI
- [ ] Reporting dashboard with charts (Chart.js / Recharts)
- [ ] Audit log viewer in UI
- [ ] Unit and integration tests
- [ ] Azure Key Vault integration
- [ ] Azure Managed Identity for services
- [ ] Application Insights monitoring
- [ ] Rate limiting and API throttling
- [ ] Password reset flow
- [ ] User management UI
