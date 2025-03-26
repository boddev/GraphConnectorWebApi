# GraphConnectorWebApi
This is a graph connector written as a dotnet webapi

This Graph Connector is a proof of concept and extracts publicly available data from the SEC and pushes it into the Microsoft Graph


To set up and run the **GraphConnectorWebApi**, you need to ensure the following prerequisites are in place:

## 1. Install .NET 8
1. Download and install the .NET 8 SDK from the official [Microsoft .NET website](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Verify the installation by running the following command in a terminal:
   ```bash
   dotnet --version
   ```
   Ensure the output shows a version starting with `8`.

---

## 2. Create an Entra ID App Registration
1. Log in to the [Microsoft Entra Admin Center](https://entra.microsoft.com/).
2. Navigate to **Azure Active Directory** > **App registrations** > **New registration**.
3. Provide the following details:
   - **Name**: Enter a name for your app (e.g., `GraphConnectorWebApi`).
   - **Supported account types**: Choose the appropriate option based on your requirements.
   - **Redirect URI**: Leave this blank for now or set it to `http://localhost` if testing locally.
4. Click **Register**.

### Add a Client Secret
1. After registration, go to the **Certificates & secrets** section.
2. Under **Client secrets**, click **New client secret**.
3. Provide a description (e.g., `GraphConnectorSecret`) and set an expiration period.
4. Click **Add** and copy the **Value** of the secret. **Save this value securely**, as it will not be shown again.

### Note the App Details
- Copy the **Application (client) ID** and **Directory (tenant) ID** from the **Overview** page. These will be needed for configuration.

---

## 3. Create an Azure Storage Account
1. Log in to the [Azure Portal](https://portal.azure.com/).
2. Navigate to **Storage accounts** > **Create**.
3. Provide the following details:
   - **Subscription**: Select your Azure subscription.
   - **Resource group**: Create a new resource group or use an existing one.
   - **Storage account name**: Enter a unique name (e.g., `graphconnectorstorage`).
   - **Region**: Choose a region close to your location.
   - **Performance**: Select `Standard`.
   - **Redundancy**: Choose the redundancy option that fits your needs (e.g., `Locally-redundant storage (LRS)`).
4. Click **Review + Create** and then **Create**.

### Note the Connection String
1. After the storage account is created, go to the **Access keys** section.
2. Copy the **Connection string** for one of the keys. This will be used in the application configuration.

---

## 4. Summary of Required Values
Ensure you have the following values ready for configuring the application:
- **Tenant ID**: From the App Registration in Entra ID.
- **Client ID**: From the App Registration in Entra ID.
- **Client Secret**: Created in the App Registration.
- **Azure Storage Connection String**: From the Azure Storage account.

Once these prerequisites are set up, you can proceed to configure and run the **GraphConnectorWebApi**.
