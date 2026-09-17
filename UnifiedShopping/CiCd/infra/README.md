# UnifiedShopping CI/CD + Azure infrastructure

Two deployment units, one pipeline shape each: **build -> test -> migrate -> staging ->
smoke test -> swap**. The API and the web front end deploy independently; the MAUI app
ships through the App Stores, not this pipeline.

```text
 push to main
      |
      v
 [build + unit tests]          dotnet build / dotnet test
      |
      v
 [publish + EF bundle]         dotnet publish -> zip; dotnet ef migrations bundle
      |
      v
 [run migrations]              efbundle --connection $SQL   (idempotent, before swap)
      |
      v
 [deploy to staging slot]      azure/webapps-deploy (gh) / AzureWebApp@1 (ado)
      |
      v
 [smoke test /health]          curl the staging slot
      |
      v
 [swap slot -> production]     az webapp deployment slot swap (atomic, zero-downtime)
```

## Folders

| Folder | What it is |
|---|---|
| [`github-workflows/`](github-workflows) | GitHub Actions: `deploy-api.yml` + `deploy-web.yml` |
| [`azure-pipelines/`](azure-pipelines) | Azure DevOps equivalent (`azure-pipelines.yml`) |
| [`infra/`](infra) | Bicep for App Services (staging slots), Azure SQL, Key Vault, App Insights |

## One-time setup (GitHub Actions)

```bash
# 1. Deploy the infrastructure
az group create -n rg-shop -l eastus
az deployment group create -g rg-shop -f CiCd/infra/main.bicep \
  -p sqlAdminPassword="$(openssl rand -base64 24)"

# 2. Create the service principal for the pipeline
az ad sp create-for-rbac --name "sp-unifiedshopping" --role Contributor \
  --scopes /subscriptions/<sub-id>/resourceGroups/rg-shop --sdk-auth
# -> store the whole JSON as the GitHub secret AZURE_CREDENTIALS

# 3. Store the SQL connection string as the GitHub secret SQL_CONNECTION_STRING
#    (prefer a Key Vault reference in real deployments)
```

## One-time setup (Azure DevOps)

1. Create an **Azure Resource Manager service connection** named e.g. `shop-azure`.
2. Create a variable group `shop-secrets` with `SQL_CONNECTION_STRING`.
3. Create two environments: `shop-staging` and `shop-production`
   (add approvals/checks on production as needed).

## Why the EF migration bundle?

`dotnet ef migrations bundle` compiles the migrations into a single self-contained
executable the pipeline runs *before* the new version receives traffic. Compared to
running migrations at app startup this:

- keeps startup fast (no migration check on every boot),
- fails the pipeline (not the site) when a migration is bad,
- stays idempotent - safe to run on every deploy.

The apps *do* still call `MigrateAsync` on startup for local SQLite convenience;
in production the bundle has already done the work, so it's a no-op.

## Zero-downtime deploys

Both apps get a `staging` slot. The pipeline deploys there, warms it, smoke-tests
`/health`, then swaps. Swap is atomic: if the smoke test fails, production never sees
the new bits. The slot's warm-up also avoids cold-start blips for the first request.

## Scaling for high transaction volume

The Bicep is deliberately minimal (P1v3, serverless SQL). For the "large amount of
transactions" target, this repo's [`TerraformAzureSite`](../../TerraformAzureSite)
folder shows the full production shape; combining both:

- **Front Door Premium + WAF** in front of the two apps (TLS, DDoS, bot rules).
- **App Service autoscale** on CPU (or KEDA rules if you containerize).
- **Azure SQL Business Critical + geo-replica** with auto-failover; the app connects
  to the failover listener. `EnableRetryOnFailure` is already wired in ShopData.
- **Output caching** is enabled on the catalog endpoints (`/api/products`); add
  Azure Redis (`AddStackExchangeRedisCache`) when you run multiple instances so
  cache + session state survive instance swaps.
- **Cart/session state**: App Service slots need a distributed cache when scaled out;
  sessions already use the cookie + provider pattern to make that swap painless.
- Consider splitting reads (catalog browsing) from writes (checkout) onto read
  replicas only when write throughput actually demands it - measure first.
