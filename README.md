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
