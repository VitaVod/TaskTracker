# TaskTracker

## Prerequisites

- Node.js 20+
- .NET SDK 9.0+
- SQL Server LocalDB or SQL Server instance

## Project Structure

- `task-tracker-web` - Angular frontend
- `task-tracker-api/TaskTracker.Api` - ASP.NET Core Web API
- `TaskTracker.sln` - Backend solution

## Frontend Commands

```bash
cd task-tracker-web
npm install
npm run build
npx ng test --watch=false --browsers=ChromeHeadless --no-progress
npm start
```

## Backend Commands

```bash
cd task-tracker-api/TaskTracker.Api
dotnet restore
dotnet build
dotnet run
```

## EF Core Migrations

A local EF tool manifest is configured at `.config/dotnet-tools.json`.

```bash
cd task-tracker-api/TaskTracker.Api
dotnet dotnet-ef migrations list
dotnet dotnet-ef database update
```

## Solution Commands

```bash
dotnet restore TaskTracker.sln
dotnet build TaskTracker.sln
dotnet test TaskTracker.sln --no-build
```
