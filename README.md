# Tasked — Backend API

The backend API for **Tasked**, a full-stack project management application that helps users organize projects, manage tasks, and collaborate with others.

Built with C# and ASP.NET Core, the API provides RESTful endpoints for application functionality, custom authentication, authorization, and persistent data storage.

## Tech Stack

- **Language:** C#
- **Framework:** ASP.NET Core Web API
- **ORM:** Entity Framework Core
- **Database:** Microsoft SQL Server
- **Authentication:** Custom identity system using JWT bearer authentication and refresh tokens
- **Containerization:** Docker and Docker Compose
- **Deployment:** Azure App Service (Linux)

## Features

- **Project Management:** Create, retrieve, update, and delete projects.
- **Task Management:** Manage tasks within projects, including assignments and status tracking.
- **Authentication:** Custom-built authentication with JWT access tokens and refresh-token-based session restoration.
- **Authorization:** Protect API endpoints and enforce access permissions for application resources.
- **Data Persistence:** Store application data in SQL Server using Entity Framework Core.
- **Database Migrations:** Manage database schema changes through EF Core code-first migrations.
- **Search and Filtering:** Query and filter application resources through API endpoints.

## Architecture

Tasked uses a client-server architecture. The frontend communicates with the backend through HTTP requests to the REST API, while the backend handles application logic, authentication, authorization, and database operations.

```text
React + TypeScript Frontend
          |
          | HTTPS / REST API
          | JWT + Refresh Cookie
          v
ASP.NET Core Web API
          |
          | Entity Framework Core
          v
      SQL Server
```

The frontend is deployed to Cloudflare Workers, while the API runs in a Docker container hosted on Azure App Service. The database is hosted separately using Azure SQL Database.

## Authentication

Tasked implements its own identity and authentication system rather than relying on ASP.NET Core Identity.

The authentication flow uses two token mechanisms:

- **Access tokens:** JWTs sent in the `Authorization: Bearer` header when accessing protected endpoints.
- **Refresh tokens:** Stored in an HTTP-only cookie and used to restore authenticated sessions and obtain new access tokens.

The frontend keeps the access token in memory, while the browser manages the refresh-token cookie. Credentialed requests allow the browser to send the cookie to the API, where refresh-token persistence and rotation are handled server-side.

## Database and Migrations

Entity Framework Core defines the application's entities and relationships and handles database access. Database schema changes are tracked through code-first migrations.

The repository includes the existing migrations needed to initialize the database. To apply pending migrations to a configured SQL Server database, run the following from the backend project directory:

```bash
dotnet ef database update
```

When making changes to the data model, generate a new migration:

```bash
dotnet ef migrations add MigrationName
```

Commit the generated migration files so the schema changes can be applied in other environments.

## Getting Started

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download)
- [Entity Framework Core CLI tools](https://learn.microsoft.com/ef/core/cli/dotnet)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) or Docker Engine with the Docker Compose plugin

### 1. Clone the Repository

```bash
git clone https://github.com/ammar-az/Tasked.git
cd Tasked
```

### 2. Configure the Application

Configure the database connection string and JWT signing key through local configuration or environment variables.

For example, ASP.NET Core configuration supports the following environment variable names:

```text
ConnectionStrings__DefaultConnection=<your-sql-server-connection-string>
Jwt__Key=<your-secret-signing-key>
```

### 3. Run the API

If you have a local SQL Server instance configured and accessible, restore dependencies and start the API:

```bash
dotnet restore
dotnet run
```

The API starts using the configured ASP.NET Core launch settings. Check the console output for its listening address and port.

## Containerization and Deployment

### Local Development with Docker Compose

Docker Compose starts the backend API and SQL Server database together using the repository's Compose configuration.

#### 1. Configure Environment Variables

Create a `.env` file in the directory containing `docker-compose.yml`:

```dotenv
TASKED_DB=Tasked
MSSQL_SA_PASSWORD=Your_Strong_Password123!
JWT_KEY=your-secret-signing-key
```

These values configure the database name, SQL Server administrator password, and JWT signing key for local development.

#### 2. Start the Database

On the first run, start SQL Server without starting the API:

```bash
docker compose up -d db
```

#### 3. Apply the Existing Migrations

Before starting the API against a fresh database, apply the migrations included in the repository:

```bash
dotnet ef database update
```

For this command, configure the EF Core connection string to reach the SQL Server container through the host port, using `localhost,1433` rather than `db,1433`. The hostname `db` is intended for connections between containers on the Compose network.

For example, the host-side connection string should follow this format:

```text
Server=localhost,1433;Database=Tasked;User Id=sa;Password=<your-MSSQL_SA_PASSWORD>;TrustServerCertificate=True
```

Use the same database name and password configured in `.env`. The .NET CLI must have access to this connection string and any other configuration required to initialize the application.

The migration files are already included in the repository, so you do not need to generate a new migration just to initialize the database.

#### 4. Start the API

Once the migrations have been applied, start the API and database:

```bash
docker compose up --build
```

The API is exposed on port `8080`, and SQL Server is exposed on port `1433`.

The Compose configuration uses a named volume to persist database data between restarts. To stop the services, press `Ctrl+C` or run:

```bash
docker compose down
```

To remove the database volume as well, run `docker compose down -v`. This also deletes the persisted database data.

### Production Deployment

The production backend is hosted on **Azure App Service for Linux** using a Docker container image published to a container image repository.

The deployment involves:

1. Building and publishing the backend Docker image.
2. Configuring Azure App Service to pull and run the image.
3. Supplying the production database connection string and JWT signing key through application settings.
4. Connecting the API to the hosted Azure SQL Database.
5. Configuring CORS and cookie settings to support the deployed frontend.

The frontend is deployed independently to Cloudflare Workers.

### CORS Configuration

The API must allow requests from the frontend's origin. For local frontend development, add the Vite development origin (`http://localhost:5173`) to the allowed origins in `Program.cs`.

For example:

```csharp
app.UseCors(policy => policy
    .WithOrigins(
        "https://tasked.ammarz.com",
        "http://localhost:5173")
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());
```

The production frontend origin must match the deployed frontend's origin exactly. When configuring CORS for credentialed requests, use explicit origins rather than `AllowAnyOrigin()`.

## Related Project

Tasked's frontend is built with React, TypeScript, and React Router.

- **Frontend Repository:** [tasked-frontend](https://github.com/ammar-az/tasked-frontend)
- **Live Application:** [Tasked](https://tasked.ammarz.com)

---

This project demonstrates full-stack application development, REST API design, custom authentication, relational data modeling, containerization, and cloud deployment.
