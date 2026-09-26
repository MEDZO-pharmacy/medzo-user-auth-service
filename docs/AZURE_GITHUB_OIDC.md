# GitHub Actions to Azure OIDC

The backend deployment workflows use GitHub OIDC. They do not need a stored Azure client secret.

## Create the Azure identity once

Run in Azure Cloud Shell while signed into the subscription containing `rg-MEDZO-NEW`:

```bash
az account set --subscription f53c5182-c196-4e12-b31f-a6e5bfa6b5fd
APP_ID=$(az ad app create --display-name medzo-github-deploy --query appId -o tsv)
az ad sp create --id "$APP_ID" --output none
SP_OBJECT_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv)
RG_SCOPE=$(az group show --name rg-MEDZO-NEW --query id -o tsv)
az role assignment create \
  --assignee-object-id "$SP_OBJECT_ID" \
  --assignee-principal-type ServicePrincipal \
  --role Contributor \
  --scope "$RG_SCOPE" \
  --output none
```

Add one federated credential for each backend workflow branch:

```bash
az ad app federated-credential create --id "$APP_ID" --parameters '{"name":"auth-main","issuer":"https://token.actions.githubusercontent.com/","subject":"repo:MEDZO-pharmacy/medzo-user-auth-service:ref:refs/heads/main","audiences":["api://AzureADTokenExchange"]}'

az ad app federated-credential create --id "$APP_ID" --parameters '{"name":"catalogue-dev","issuer":"https://token.actions.githubusercontent.com/","subject":"repo:MEDZO-pharmacy/medzo-catalogue-inventory-service:ref:refs/heads/dev","audiences":["api://AzureADTokenExchange"]}'

TENANT_ID=$(az account show --query tenantId -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
printf 'AZURE_CLIENT_ID=%s\nAZURE_TENANT_ID=%s\nAZURE_SUBSCRIPTION_ID=%s\n' "$APP_ID" "$TENANT_ID" "$SUBSCRIPTION_ID"
```

`APP_ID`, tenant ID, and subscription ID are identifiers, not passwords. Do not create a client secret for this setup.

## Add GitHub repository variables

In each backend repository, open **Settings → Secrets and variables → Actions → Variables** and create:

| Variable | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | `APP_ID` printed above |
| `AZURE_TENANT_ID` | `TENANT_ID` printed above |
| `AZURE_SUBSCRIPTION_ID` | `SUBSCRIPTION_ID` printed above |

The federated subject must match the workflow branch exactly: auth uses `main`; Catalogue/Inventory uses `dev`. The Azure identity needs Contributor only on `rg-MEDZO-NEW`.

After adding these variables, re-run **Test and Deploy Auth Service** and **Test and Deploy Catalogue and Inventory Service** from their Actions pages. Each run tests, publishes the immutable GHCR image, signs into Azure, deploys, and checks `/health`.
