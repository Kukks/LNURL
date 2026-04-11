# LNURL .NET — API Reference

Welcome to the API documentation for the **LNURL** .NET library, a comprehensive implementation of the [LNURL protocol](https://github.com/lnurl/luds) for the Bitcoin Lightning Network.

## Packages

| Package | Description |
|---------|-------------|
| **LNURL.Core** | Core types and System.Text.Json support (no Newtonsoft.Json runtime dependency) |
| **LNURL** | Full package — re-exports everything from Core with Newtonsoft.Json runtime support |

## Getting Started

```bash
# System.Text.Json only (lighter dependency)
dotnet add package LNURL.Core

# Or: with Newtonsoft.Json support
dotnet add package LNURL
```

Then start with the main entry point — the [`LNURL`](api/LNURL.LNURL.html) static class:

```csharp
var result = await LNURL.LNURL.FetchInformation(uri, httpClient);
```

## Core Classes

| Class | Description |
|-------|-------------|
| [`LNURL`](api/LNURL.LNURL.html) | Main entry point — parse, encode, and fetch LNURL endpoints |
| [`LNURLPayRequest`](api/LNURL.LNURLPayRequest.html) | LNURL-pay flow (LUD-06) |
| [`LNURLWithdrawRequest`](api/LNURL.LNURLWithdrawRequest.html) | LNURL-withdraw flow (LUD-03) |
| [`LNAuthRequest`](api/LNURL.LNAuthRequest.html) | LNURL-auth (LUD-04) |
| [`LNURLChannelRequest`](api/LNURL.LNURLChannelRequest.html) | LNURL-channel (LUD-02) |
| [`LNURLVerifyResponse`](api/LNURL.LNURLVerifyResponse.html) | Payment verification (LUD-21) |
| [`LNURLJsonOptions`](api/LNURL.LNURLJsonOptions.html) | System.Text.Json support |
| [`BoltCardHelper`](api/LNURL.BoltCardHelper.html) | NFC BoltCard support |

## JSON Serialization

The library supports both **Newtonsoft.Json** (via `[JsonProperty]` attributes, requires the `LNURL` package) and **System.Text.Json** (via [`LNURLJsonOptions`](api/LNURL.LNURLJsonOptions.html), works with either package).

## Source Code

[GitHub Repository](https://github.com/Kukks/LNURL)
