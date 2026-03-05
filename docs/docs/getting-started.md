# Getting Started

This guide walks you through cloning the repository, configuring Microsoft Entra (Azure AD) authentication, and running the application locally.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PowerShell 7+](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) (`pwsh`)
- A Microsoft Entra tenant (formerly Azure Active Directory)  
  A [free Azure account](https://azure.microsoft.com/free/) includes a default Entra tenant.

---

## 1. Clone and restore

```pwsh
git clone https://github.com/Sannel/Encoding.Manager.git
cd Encoding.Manager
dotnet restore
```

---

## 2. Register the application in Microsoft Entra

1. Sign in to the [Azure portal](https://portal.azure.com).
2. Navigate to **Microsoft Entra ID** → **App registrations** → **New registration**.
3. Fill in the form:
   - **Name**: `Encoding Manager` (or any display name you prefer)
   - **Supported account types**: choose the option that matches your organisation's requirements  
     (typically *Accounts in this organizational directory only*)
   - **Redirect URI**: select **Web** and enter:
     ```
     https://localhost:7000/signin-oidc
     ```
4. Click **Register**.
5. On the **Overview** page, copy:
   - **Application (client) ID** → this is your `ClientId`
   - **Directory (tenant) ID** → this is your `TenantId`
6. Navigate to **Authentication** and add a **Front-channel logout URL**:
   ```
   https://localhost:7000/signout-callback-oidc
   ```
   Also ensure **ID tokens** is checked under *Implicit grant and hybrid flows*, then click **Save**.
7. Navigate to **Certificates & secrets** → **Client secrets** → **New client secret**.
   - Add a description and expiry, then click **Add**.
   - **Copy the secret value immediately** — it is only shown once.

---

## 3. Configure appsettings

Open `src/Sannel.Encoding.Manager.Web/appsettings.json` (or `appsettings.Development.json` for local overrides) and fill in the placeholders with the values from the previous step:

```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "Domain": "yourdomain.onmicrosoft.com",
  "TenantId": "<Directory (tenant) ID>",
  "ClientId": "<Application (client) ID>",
  "CallbackPath": "/signin-oidc",
  "SignedOutCallbackPath": "/signout-callback-oidc"
}
```

> **Never commit `ClientSecret` to source control.** Store it with .NET User Secrets (see below).

---

## 4. Store the client secret with .NET User Secrets

User Secrets keep sensitive values out of `appsettings.json` and source control. They are automatically loaded in the `Development` environment.

```pwsh
# From the repository root
dotnet user-secrets set "AzureAd:ClientSecret" "<your-secret-value>" `
    --project src/Sannel.Encoding.Manager.Web
```

Verify the secret was saved:

```pwsh
dotnet user-secrets list --project src/Sannel.Encoding.Manager.Web
```

You should see output similar to:

```
AzureAd:ClientSecret = <your-secret-value>
```

> User Secrets are stored at  
> `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`  
> and are never included in builds or published output.

---

## 5. Run the application

```pwsh
dotnet run --project src/Sannel.Encoding.Manager.Web
```

Navigate to `https://localhost:7000`. You will be redirected to the Microsoft Entra sign-in page. After authenticating with an account in your configured tenant you will be returned to the application.