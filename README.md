# backend
Backend app with data features



for some cases of service management:
Run the below command on the server:
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
Start-Service sshd; Set-Service -Name sshd -StartupType Automatic


On the server (elevated PowerShell)

Download SSH from: https://github.com/PowerShell/Win32-OpenSSH/releases

# 1. Clean up any leftover service from the Downloads attempt
Stop-Service sshd -ErrorAction SilentlyContinue
sc.exe delete sshd
sc.exe delete ssh-agent   # ignore "service does not exist"

# 2. Copy the extracted build into Program Files (secured ACLs)
Copy-Item "C:\Users\spinadmin\Downloads\OpenSSH-Win64\OpenSSH-Win64" "C:\Program Files\OpenSSH" -Recurse -Force
Set-Location "C:\Program Files\OpenSSH"

# 3. Register the services properly (this is what was missing)
powershell -ExecutionPolicy Bypass -File .\install-sshd.ps1

# 4. Make sure host keys exist and have correct permissions
& .\ssh-keygen.exe -A
powershell -ExecutionPolicy Bypass -File .\FixHostFilePermissions.ps1 -Confirm:$false

# 5. Start + auto-start
Set-Service sshd -StartupType Automatic
Start-Service sshd

# 6. Firewall (if not already present)
New-NetFirewallRule -Name sshd -DisplayName 'OpenSSH Server (sshd)' `
  -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22
┌──────────────┬───────────────────────────┬───────────────────────────┐
│ Area         │ View Role                 │ Edit Role                 │
├──────────────┼───────────────────────────┼───────────────────────────┤
│ Status pages │ {CompanyId}_VIEW_STATUS   │ {CompanyId}_EDIT_STATUS   │
├──────────────┼───────────────────────────┼───────────────────────────┤
│ Data pages   │ {CompanyId}_VIEW_DATA     │ {CompanyId}_EDIT_DATA     │
├──────────────┼───────────────────────────┼───────────────────────────┤
│ Admin pages  │ {CompanyId}_VIEW_ADMIN    │ {CompanyId}_EDIT_ADMIN    │
└──────────────┴───────────────────────────┴───────────────────────────


─────────────────────┬──────────────────────────┬─────────────────────────────────────┐
│ Page                │ Access Denied if no VIEW │ Edit buttons hidden if no EDIT      │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ AssetDetails        │ ✅                       │ ✅ Edit/Dependencies/Status buttons │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ AssetDependencies   │ ✅                       │ ✅ Add/remove dependency UI         │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ AssetForm           │ —                        │ ✅ Whole form blocked               │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ IncidentDetails     │ ✅                       │ ✅ Edit/Resolve/Update buttons      │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ IncidentForm        │ —                        │ ✅ Whole form blocked               │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ DataWarehouse       │ ✅                       │ —                                   │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ ViewData            │ ✅                       │ —                                   │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ ListDatasets        │ ✅                       │ ✅ New/Edit/Delete/Create buttons   │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ CreateDataset       │ —                        │ ✅ Whole form blocked               │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ EditDataset         │ —                        │ ✅ Whole form blocked               │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ ImportDataPage      │ —                        │ ✅ Whole page blocked               │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ ListTable           │ ✅                       │ ✅ New Table button                 │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ CreateTable         │ —                        │ ✅ Whole form blocked               │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ Admin/Home          │ ✅ VIEW_ADMIN            │ ✅ Add button requires EDIT_ADMIN   │
├─────────────────────┼──────────────────────────┼─────────────────────────────────────┤
│ Admin/AllUsers      │ ✅ VIEW_ADMIN            │ —                                   │
└─────────────────────┴──────────────────────────┴─────────────────────────────────────┘


only the users with role {COMPANYID}_DATASETS can read and WRITE data/datasets pages
only the users with role {COMPANYID}_DATA_WAREHOUSE can read the datawarehouse page
only the users with role {COMPANYID}_METRICS_READ can read the metrics page
only the users with role {COMPANYID}_METRICS_WRITE can read and write the metrics page
only the users with role {COMPANYID}_SALES can read realtime/sales-dashboard page
only the users with role {COMPANYID}_STATUS_READ can read the status, status/entities and status/incidents pages
only the users with role {COMPANYID}_INCEDENTS can read and write the status, status/entities and status/incidents pages
the user with role {COMPANYID}_ADMIN can do everything


## Power BI setup
For the app-only (client-credentials) flow you're using, you do NOT grant Power BI API permissions on the Azure app registration. Service-principal access to Power BI is governed by two Power BI settings, not by Azure AD API permissions. The "identity None ... insufficient privileges" message almost always means one of the two below is missing.

1. Enable se

- Go to app.powerbi.com → Settings (gear) → Admin portal → Tenant settings → Developer settings.
- Enable "Allow service principals to use Power BI APIs".
- Set it to Specific security groups, create/choose an Entra security group, and add your app (service principal) as a member of that group.
- This can take ~15 minutes to propagate.

2. Give the service principal access to the workspace

- Open the workspace that contains the dataset → Manage access (Access).
- Add your app by name/client ID as Contributor or Member (Contributor is enough to trigger refreshes).

3. Azure App Registration — what to actually do there

- Keep the Client ID, Tenant ID, and a Client secret (under Certificates & secrets) — which you already have.
- Under API permissions: leave the Power BI Service permissions empty. App-only tokens use the https://analysis.windows.net/powerbi/api/.default scope and get their authorization from steps 1 & 2 above, not from API-permission grants.
  - (Those Dataset.ReadWrite.All-style permissions only apply to the delegated / signed-in-user flow, which isn't what this uses.)

A couple of gotchas

- The workspace must be a modern workspace (not a "classic" v1 workspace) — service principals don't work with classic ones.
- A refresh will only succeed if the dataset's data source credentials/gateway are already configured in Power BI; the API just queues it.

Once steps 1 and 2 are done, retry Refresh Now — the same identity (c9dc1e5a-…) should now be accepted. If you still get 403 after ~15 min, double-check the service principal is actually a member of the security group named in the tenant setting (that's the most common miss).