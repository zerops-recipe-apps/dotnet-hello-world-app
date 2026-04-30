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

Single ASP.NET app with optional `wwwroot/`. Tilde extraction (`./out/~`) strips the `out/` prefix so static files land at `/var/www/wwwroot/` (where ASP.NET's `ContentRootPath = Directory.GetCurrentDirectory()` expects them).

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
<!-- #ZEROPS_EXTRACT_END:integration-guide# -->


<!-- #ZEROPS_EXTRACT_START:knowledge-base# -->
### Gotchas

- **Hand-written `.csproj` needs `<ImplicitUsings>enable</ImplicitUsings>` + `<Nullable>enable</Nullable>`** — both ship by default in `dotnet new` templates; a minimal manual `.csproj` that skips them fails the build with `CS0246: type not found` for `List<T>`/`Task`/`HttpClient`/etc. (no implicit usings) and floods CS8600/CS8602 on net9-style code (no nullable analyzer config).
- **Razor view changes (`.cshtml`) need a process restart in dev mode** — ASP.NET only compiles Razor at build time by default, so editing a `.cshtml` over SSHFS (`/var/www/Pages/...`) doesn't take effect until you restart `dotnet run`. Two ways out: run `dotnet watch run` instead of `dotnet run` (file-watcher restarts the process automatically on changes), or add the `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` package and call `services.AddMvc().AddRazorRuntimeCompilation()` (or `AddControllersWithViews().AddRazorRuntimeCompilation()`) in `Program.cs` so views recompile per request. `dotnet watch run` is faster for tight iteration; runtime compilation is safer when SSHFS file-watcher events don't propagate cleanly.
- **`DbContext.EnsureCreated()` no-ops on a populated database** — the method only creates schema if the database has zero tables. When the managed `db` is shared across stacks (another project's Laravel app already created `users`, `sessions`, `cache_locks`, etc.), `EnsureCreated` from .NET sees existing tables and does nothing — the app then crashes with `42P01: relation "<YourTable>" does not exist` on the first query. Use proper migrations (`dotnet ef migrations add Initial && dotnet ef database update`, executed via the migration variant in §2) or run `CREATE TABLE IF NOT EXISTS` directly. `EnsureCreated` is a dev-only convenience and doesn't compose with shared databases.
<!-- #ZEROPS_EXTRACT_END:knowledge-base# -->