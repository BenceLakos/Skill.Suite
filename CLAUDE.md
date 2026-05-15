# CLAUDE.md - Skill Suite

## Overview
Skill Suite manages competition entities, including competitors, experts, sessions, docker services, file access and credentials. It consumes API webhooks from Git to run judgement docker services to mark each submission for each competitor. It provides an extensive UI for managing all entities and viewing results with separation for admins and competitors.

## Tech Stack
- .NET 10, ASP.NET Core Minimal APIs
- Entity Framework Core 10 with PostgreSQL
- Mediator for CQRS (source-generated, https://github.com/martinothamar/Mediator)
- FluentValidation for request validation
- Scalar for OpenAPI documentation
- Mapperly for object mapping
- Identity for authentication and authorization
- MudBlazor for UI

## Project Structure
- `Skill.Suite/` - Endpoints, UIs, middleware, DI configuration
- `Skill.Suite.Application/` - Commands, queries, handlers, validators
- `Skill.Suite.Domain/` - Entities, value objects, enums, domain events
- `Skills.Suite.Infra/` - EF Core, external services, repositories

## Commands
- Build: `dotnet build`
- Run project: `dotnet run --project Skill.Suite`
- Add Migration: `dotnet ef migrations add <Name> -p Skill.Suite.Infra -s Skill.Suite`
- Update Database: `dotnet ef database update -p Skill.Suite.Infra -s Skill.Suite`
- Format: `dotnet format`

## Architecture Rules
- Domain layer has ZERO external dependencies
- Application layer defines interfaces, Infrastructure implements them
- All database access goes through EF Core DbContext (no repository pattern)
- Use Mediator for all command/query handling
- API layer is thin - endpoint definitions only
- UI components should be reusable and not contain business logic

## Code Conventions

### Naming
- Commands: `Create[Entity]Command`, `Update[Entity]Command`
- Queries: `Get[Entity]Query`, `List[Entities]Query`
- Handlers: `[Command/Query]Handler`
- DTOs: `[Entity]Dto`, `Create[Entity]Request`, `Create[Entity]Response`

### Patterns We Use
- Primary constructors for DI
- Records for DTOs and commands
- Result<T> pattern for error handling (no exceptions for flow control)
- File-scoped namespaces (Always put the namespace declaration at the top, then the imports)
- Always pass CancellationToken to async methods
- Mapperly mappers for object mapping (https://mapperly.riok.app)

### Patterns We DON'T Use (Never Suggest)
- Repository pattern (use EF Core directly)
- Exceptions for business logic errors
- Stored procedures
- Two classes in a single file, even if they are small
- Commenting every line of code (code should be self-explanatory, use meaningful names instead)
- Magic strings or numbers (use constants or enums instead)

## Validation
- All request validation in FluentValidation validators
- Validators auto-registered via assembly scanning
- Validation runs in Mediator pipeline behavior

## Git Workflow
- Branch naming: `feature/`, `bugfix/`, `hotfix/`
- Commit format: `type: description` (feat, fix, refactor, test, docs)
- Commit description one line, no more than 72 characters
- Never include co-author in the commit message, use PR description instead
- Always create a branch before changes
