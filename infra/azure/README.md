# Azure Deployment Reference

This folder contains an Azure-ready deployment reference for the assessment.
It is intentionally small enough to review during a take-home evaluation while
still covering the production concerns that matter: isolated runtime services,
managed secret storage, health probes, scaling rules and centralized logs.

## Resources

- Azure Container Apps Environment.
- Internal API Container App.
- Public frontend Container App.
- Azure SQL Database.
- Key Vault for database, JWT and assessment credential secrets.
- User-assigned managed identities.
- Log Analytics workspace.

The frontend image reads `API_UPSTREAM` at container startup, so the same image
works in Docker Compose and in cloud environments.

## Deploy Manually

Create a resource group and deploy the Bicep template:

```bash
az group create --name rg-fundo-loans-preview --location eastus
az deployment group create \
  --resource-group rg-fundo-loans-preview \
  --template-file infra/azure/main.bicep \
  --parameters @infra/azure/main.parameters.example.json
```

Do not place real secret values in `main.parameters.example.json`. Pass them
through GitHub environment secrets, a local untracked parameter file or Azure
Key Vault integration.

## Production Hardening Notes

- Replace the SQL public firewall rule with private networking when the target
  Azure subscription and DNS model are known.
- Run schema migrations through a deployment job before shifting traffic.
- Use GitHub OIDC for Azure login instead of long-lived cloud credentials.
- Keep preview and production as separate GitHub environments with approvals.
- Rotate assessment credentials after each review cycle.
