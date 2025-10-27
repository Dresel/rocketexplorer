# Introduction

Rocket Explorer is an open-source community project for analyzing and visualizing the decentralized [Rocket Pool](https://rocketpool.net) network.

It is inspired by **rocketscan.io**, which is no longer actively maintained, and builds on ideas shared by the Rocket Pool community. The project offers insight into the Rocket Pool network, with a focus on the upcoming Saturn release, which builds on the [tokenomics rework](https://rpips.rocketpool.net/tokenomics-2024).

For questions, feedback, or suggestions, feel free to join the discussion on [Discord](https://discord.gg/rocketpool)

## Technical Overview

Rocket Explorer follows an **API-less, event-driven architecture**.  
All data is stored and served from an **object storage backend (S3-compatible)**.  
The system consists of two main components: a **static web app frontend** and a **serverless, timer-triggered function** responsible for event processing and data storage.

### Backend

The backend runs on **Azure Functions** and performs **incremental event processing** using **Nethereum** to read on-chain events and data.  
Processed results are serialized with **MessagePack** and stored as blobs in an **Hetzner S3 Object Storage**, ready to be consumed by the frontend.

### Frontend

The frontend is a **Blazor Static Web App** using **MudBlazor** with a **Material 3-inspired design** and **LiveCharts2** for data visualization deployed as **Azure Static Web App**. 
All content is loaded directly from the pre-generated **MessagePack blobs** in object storage.

## Local Development

For local event processing, you can use the `RocketExplorer.Bootstrap` project.  
You will need to configure an S3-compatible storage (e.g. **MinIO** via Docker) and provide an RPC URL to an **archive node** in your `appsettings.json`.  
The environment can be selected by setting the `"Environment": "Devnet"` key/value pair.

The Blazor application determines its configuration based on the subdomain.  
To override this behavior, the simplest approach is to set the environment explicitly in `Configuration.cs`:

```csharp
// ... subdomain handling

Environment = Environment.Devnet; // Set your desired environment here

Network = Environment switch
{
    Environment.LocalDevnet => Network.Hoodi,
    Environment.Devnet => Network.Hoodi,
    Environment.LocalTestnet => Network.Hoodi,
    Environment.Testnet => Network.Hoodi,
    Environment.LocalMainnet => Network.Mainnet,
    Environment.Mainnet => Network.Mainnet,
    _ => throw new InvalidOperationException("Network is null"),
};
```

You may also need to adjust the base URL for the S3 storage in the same file:
```csharp
public string ObjectStoreBaseUrl => $"https://rocketexplorer.nbg1.your-objectstorage.com/{ObjectStoreBucketName}";
```
