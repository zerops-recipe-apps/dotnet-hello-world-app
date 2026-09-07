# dotnet-hello-world-app

Minimal .NET 9 ASP.NET Core web app with a health-check endpoint that queries PostgreSQL via Npgsql, demonstrating database connectivity on Zerops.

## Zerops service facts

- HTTP port: `8080`
- Siblings: `db` (PostgreSQL) — env: `DB_HOST`, `DB_PORT`, `DB_USER`, `DB_PASS`, `DB_NAME`
- Runtime base: `dotnet@9`

## Zerops dev

`setup: dev` idles on `zsc noop --silent`; the agent starts the dev server.

- Dev command: `dotnet run --project App/App.csproj`
- In-container rebuild without deploy: `dotnet publish App/App.csproj -c Release -o out/app`

**All platform operations (start/stop/status/logs of the dev server, deploy, env / scaling / storage / domains) go through the Zerops development workflow via `zcp` MCP tools. Don't shell out to `zcli`.**

## Notes

- NuGet package cache (`~/.nuget/packages`) lives in the build container — after SSH in dev, run `dotnet restore App/App.csproj` before `dotnet run`.
- `HOME=/home/zerops` is set in dev runtime so the .NET CLI can locate its caches and temp dirs.
- Kestrel binds via `ASPNETCORE_URLS=http://0.0.0.0:8080`; don't override with `--urls` locally.
