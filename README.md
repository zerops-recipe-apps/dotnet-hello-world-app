# .NET Hello World Recipe App

<!-- #ZEROPS_EXTRACT_START:intro# -->
Minimal [.NET 9](https://dotnet.microsoft.com/) web application with a health check endpoint
and automatic PostgreSQL migration, demonstrating how to connect and verify a database on
[Zerops](https://zerops.io). Used within [.NET Hello World recipe](https://app.zerops.io/recipes/dotnet-hello-world) for [Zerops](https://zerops.io) platform.
<!-- #ZEROPS_EXTRACT_END:intro# -->

⬇️ **Full recipe page and deploy with one-click**

[![Deploy on Zerops](https://github.com/zeropsio/recipe-shared-assets/blob/main/deploy-button/light/deploy-button.svg)](https://app.zerops.io/recipes/dotnet-hello-world?environment=small-production)

![dotnet cover](https://github.com/zeropsio/recipe-shared-assets/blob/main/covers/svg/cover-dotnet.svg)

## Integration Guide

<!-- #ZEROPS_EXTRACT_START:integration-guide# -->

### 1. Adding `zerops.yaml`

The main application configuration file you place at the root of your
repository. It tells Zerops how to build, deploy, and run your
application.

The default layout below targets the most common case: a single ASP.NET
application with optional `wwwroot/` static assets. Tilde extraction
(`./out/~`) strips the `out/` prefix so static files land at the same
level ASP.NET's default `ContentRootPath = Directory.GetCurrentDirectory()`
expects (`/var/www/wwwroot/`). If you need a database migration step
before app start, use the multi-app variant in Section 2.

```yaml
zerops:
  # Production setup — single-app default (extract contents of ./out
  # into /var/www so ASP.NET's ContentRootPath + wwwroot/ line up).
  - setup: prod
    build:
      base: dotnet@9

      buildCommands:
        # Publish framework-dependent artifacts (runtime provided by
        # the dotnet@9 container image — no bundled runtime needed).
        # dotnet publish implicitly restores NuGet packages first.
        - dotnet publish App.csproj -c Release -o out

      # Extract CONTENTS of ./out into /var/www (tilde strips the
      # `out/` prefix). Runtime sees /var/www/App.dll and
      # /var/www/wwwroot/ at the expected level. Without tilde, paths
      # would land at /var/www/out/ and ASP.NET's wwwroot lookup
      # (ContentRootPath = /var/www, default) would 404 static assets.
      deployFiles:
        - ./out/~

      # cache: true snapshots the global NuGet package cache
      # (~/.nuget/packages) in the build container image, so
      # subsequent builds skip re-downloading packages.
      cache: true

    # Readiness check: verifies new containers respond before the
    # project balancer routes traffic to them (zero-downtime deploy).
    deploy:
      readinessCheck:
        httpGet:
          port: 8080
          path: /

    run:
      base: dotnet@9

      ports:
        - port: 8080
          httpSupport: true

      envVariables:
        ASPNETCORE_ENVIRONMENT: Production
        # Kestrel listens on all interfaces at port 8080.
        ASPNETCORE_URLS: http://0.0.0.0:8080

      # CWD is /var/www at start; App.dll was extracted there by the
      # tilde. ContentRootPath = Directory.GetCurrentDirectory() =
      # /var/www, so wwwroot at /var/www/wwwroot is discovered.
      start: dotnet App.dll

  # Development setup — deploy full source code so the developer can
  # SSH in and run the app interactively. NuGet packages are restored
  # in the build container to populate the cache; the developer runs
  # 'dotnet restore' again after SSH (runtime container starts fresh).
  #
  # Self-deploy (source == target) requires deployFiles: ./ —
  # narrower patterns destroy the working tree on artifact extraction.
  - setup: dev
    build:
      base: dotnet@9

      buildCommands:
        # Restore packages to populate the NuGet cache in the build
        # container (cached via cache: true for subsequent builds).
        # The developer will need to run 'dotnet restore' after SSH —
        # NuGet cache lives in the build container, not the runtime.
        - dotnet restore App.csproj

      # Deploy the entire working directory — source code, project
      # files, and zerops.yaml (so 'zcli push' works from SSH).
      deployFiles: ./

      cache: true

    run:
      base: dotnet@9

      ports:
        - port: 8080
          httpSupport: true

      envVariables:
        ASPNETCORE_ENVIRONMENT: Development
        ASPNETCORE_URLS: http://0.0.0.0:8080
        # HOME is required — .NET CLI and NuGet use it to locate
        # package caches and temp dirs in the runtime container.
        HOME: /home/zerops

      # Container stays idle — developer starts the app via SSH:
      #   dotnet restore App.csproj
      #   dotnet run --project App.csproj
      start: zsc noop --silent
```

### 2. Variant — with database migration

When the application needs a one-shot migration step before the server
starts (e.g. EF Core migrations against PostgreSQL), publish the app
and the migrator to separate subdirectories and preserve the `out/`
structure. Note the two differences from the default:

- `deployFiles: ./out` (preserve; no tilde) — keeps `out/{app,migrate}/`
  structure so `initCommands` can reference `/var/www/out/migrate/`.
- `start: cd /var/www/out/app && dotnet App.dll` — changes CWD so
  ASP.NET's `ContentRootPath` aligns with the app directory, letting
  `wwwroot/` at `/var/www/out/app/wwwroot/` be discovered correctly.

```yaml
zerops:
  - setup: prod
    build:
      base: dotnet@9
      buildCommands:
        - dotnet publish App/App.csproj -c Release -o out/app
        - dotnet publish Migrate/Migrate.csproj -c Release -o out/migrate
      # Preserve directory — artifact lands at /var/www/out/{app,migrate}/.
      deployFiles:
        - ./out
      cache: true

    deploy:
      readinessCheck:
        httpGet:
          port: 8080
          path: /

    run:
      base: dotnet@9
      # zsc execOnce keys migration execution to the deploy version —
      # only one container runs the migrator, all others wait.
      initCommands:
        - zsc execOnce ${appVersionId} --retryUntilSuccessful -- dotnet /var/www/out/migrate/Migrate.dll
      ports:
        - port: 8080
          httpSupport: true
      envVariables:
        ASPNETCORE_ENVIRONMENT: Production
        ASPNETCORE_URLS: http://0.0.0.0:8080
        DB_NAME: db
        DB_HOST: ${db_hostname}
        DB_PORT: ${db_port}
        DB_USER: ${db_user}
        DB_PASS: ${db_password}
      # cd to the app directory so CWD — and therefore ContentRootPath —
      # points at /var/www/out/app/, where wwwroot/ lives.
      start: cd /var/www/out/app && dotnet App.dll

  - setup: dev
    build:
      base: dotnet@9
      buildCommands:
        - dotnet restore App/App.csproj
        - dotnet restore Migrate/Migrate.csproj
      deployFiles: ./
      cache: true
    run:
      base: dotnet@9
      initCommands:
        - zsc execOnce ${appVersionId} --retryUntilSuccessful -- dotnet run --project Migrate/Migrate.csproj
      ports:
        - port: 8080
          httpSupport: true
      envVariables:
        ASPNETCORE_ENVIRONMENT: Development
        ASPNETCORE_URLS: http://0.0.0.0:8080
        HOME: /home/zerops
        DB_NAME: db
        DB_HOST: ${db_hostname}
        DB_PORT: ${db_port}
        DB_USER: ${db_user}
        DB_PASS: ${db_password}
      start: zsc noop --silent
```

### 3. Deploy-mode semantics

This recipe demonstrates both canonical deploy classes:

- **Self-deploy** (`zerops_deploy targetService=appdev`) uses the DEV
  block's `deployFiles: ./` — ships the whole source tree. Narrower
  patterns would destroy the dev workspace on extraction (DM-2 in
  Zerops' spec).
- **Cross-deploy** (`zerops_deploy sourceService=appdev
  targetService=appstage`) uses the PROD block's `deployFiles` to
  cherry-pick build output. Default is tilde extraction (`./out/~`)
  for simple single-app layouts; the migration variant uses preserve
  (`./out`) with explicit `cd` in start.

Tilde vs preserve is a runtime content-root decision: ASP.NET's default
`ContentRootPath` is `Directory.GetCurrentDirectory()` (= `/var/www` at
platform start), so static assets must end up at that level. Tilde
extraction achieves this for single-app layouts; `cd` in the start
command achieves it for multi-app layouts.
<!-- #ZEROPS_EXTRACT_END:integration-guide# -->
