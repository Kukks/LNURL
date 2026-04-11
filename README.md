# LNURL

A comprehensive .NET implementation of the [LNURL protocol](https://github.com/lnurl/luds) for the Bitcoin Lightning Network.

[![NuGet](https://img.shields.io/nuget/v/LNURL.svg)](https://www.nuget.org/packages/LNURL)
[![NuGet LNURL.Core](https://img.shields.io/nuget/v/LNURL.Core.svg?label=LNURL.Core)](https://www.nuget.org/packages/LNURL.Core)
[![Build](https://github.com/Kukks/LNURL/actions/workflows/ci.yml/badge.svg)](https://github.com/Kukks/LNURL/actions/workflows/ci.yml)
[![API Docs](https://img.shields.io/badge/docs-API%20Reference-blue)](https://kukks.github.io/LNURL/)

## Packages

| Package | Description | Dependencies |
|---------|-------------|--------------|
| **[LNURL](https://www.nuget.org/packages/LNURL)** | Full package with Newtonsoft.Json runtime support | LNURL.Core + Newtonsoft.Json |
| **[LNURL.Core](https://www.nuget.org/packages/LNURL.Core)** | Core types and System.Text.Json support only | BTCPayServer.Lightning.Common |

### Which package should I use?

- **Use `LNURL`** if you already use Newtonsoft.Json in your project, or want backward compatibility with previous versions.
- **Use `LNURL.Core`** if you want to avoid the Newtonsoft.Json transitive dependency and prefer System.Text.Json only.

Both packages share the same namespace (`LNURL`) and identical class/property names. Switching between them requires only a package reference change, not code changes.

## Installation

```bash
# Full package (includes Newtonsoft.Json support)
dotnet add package LNURL

# Or: Core package only (no Newtonsoft.Json runtime dependency)
dotnet add package LNURL.Core
```

## Supported LUD Specifications

| LUD | Feature | Class |
|-----|---------|-------|
| [LUD-01](https://github.com/lnurl/luds/blob/luds/01.md) | Bech32 encoding | `LNURL` |
| [LUD-02](https://github.com/lnurl/luds/blob/luds/02.md) | Channel request | `LNURLChannelRequest` |
| [LUD-03](https://github.com/lnurl/luds/blob/luds/03.md) | Withdraw request | `LNURLWithdrawRequest` |
| [LUD-04](https://github.com/lnurl/luds/blob/luds/04.md) | Auth (login) | `LNAuthRequest` |
| [LUD-06](https://github.com/lnurl/luds/blob/luds/06.md) | Pay request | `LNURLPayRequest` |
| [LUD-07](https://github.com/lnurl/luds/blob/luds/07.md) | Hosted channel | `LNURLHostedChannelRequest` |
| [LUD-09](https://github.com/lnurl/luds/blob/luds/09.md) | Success actions | `ILNURLPayRequestSuccessAction` |
| [LUD-10](https://github.com/lnurl/luds/blob/luds/10.md) | AES success action | `LNURLPayRequestSuccessActionAES` |
| [LUD-11](https://github.com/lnurl/luds/blob/luds/11.md) | Disposable invoices | `LNURLPayRequestCallbackResponse.Disposable` |
| [LUD-12](https://github.com/lnurl/luds/blob/luds/12.md) | Comments | `LNURLPayRequest.CommentAllowed` |
| [LUD-14](https://github.com/lnurl/luds/blob/luds/14.md) | Balance check | `LNURLWithdrawRequest.BalanceCheck` |
| [LUD-15](https://github.com/lnurl/luds/blob/luds/15.md) | Balance notify | `LNURLWithdrawRequest.SendRequest` |
| [LUD-16](https://github.com/lnurl/luds/blob/luds/16.md) | Lightning Address | `LNURL.FetchPayRequestViaInternetIdentifier` |
| [LUD-17](https://github.com/lnurl/luds/blob/luds/17.md) | URI schemes | `LNURL.Parse` / `LNURL.EncodeUri` |
| [LUD-18](https://github.com/lnurl/luds/blob/luds/18.md) | Payer data | `LNURLPayRequest.LUD18PayerData` |
| [LUD-19](https://github.com/lnurl/luds/blob/luds/19.md) | Pay/withdraw links | `PayLink` / `WithdrawLink` properties |
| [LUD-21](https://github.com/lnurl/luds/blob/luds/21.md) | Verify (payment proof) | `LNURLVerifyResponse` |

Also includes **BoltCard** support for NFC hardware wallet authentication (`BoltCardHelper`).

## Quick Start

### Parse and Fetch an LNURL

```csharp
using LNURL;

var httpClient = new HttpClient();

// Parse a bech32-encoded LNURL
var uri = LNURL.LNURL.Parse(
    "LNURL1DP68GURN8GHJ7UM9WFMXJCM99E3K7MF0V9CXJ0M385EKVCENXC6R2C35...",
    out var tag);

// Fetch and get the typed response
var result = await LNURL.LNURL.FetchInformation(uri, httpClient);

switch (result)
{
    case LNURLPayRequest payRequest:
        Console.WriteLine($"Pay {payRequest.MinSendable} - {payRequest.MaxSendable} msats");
        break;
    case LNURLWithdrawRequest withdrawRequest:
        Console.WriteLine($"Withdraw up to {withdrawRequest.MaxWithdrawable} msats");
        break;
    case LNAuthRequest authRequest:
        Console.WriteLine($"Auth challenge: {authRequest.K1}");
        break;
}
```

### Lightning Address (LUD-16)

```csharp
// Fetch a pay request from a Lightning Address
var payRequest = await LNURL.LNURL.FetchPayRequestViaInternetIdentifier(
    "user@wallet.example.com", httpClient);

Console.WriteLine($"Pay {payRequest.MinSendable} - {payRequest.MaxSendable} msats");
```

### LNURL-Pay (LUD-06)

```csharp
// After obtaining a pay request, send a payment
var callbackResponse = await payRequest.SendRequest(
    amount: new LightMoney(10000),
    network: Network.Main,
    httpClient: httpClient,
    comment: "Great work!");

// callbackResponse.Pr contains the BOLT11 invoice to pay
Console.WriteLine($"Invoice: {callbackResponse.Pr}");

// Handle success actions (LUD-09)
switch (callbackResponse.SuccessAction)
{
    case LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionMessage msg:
        Console.WriteLine(msg.Message);
        break;
    case LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionUrl url:
        Console.WriteLine($"{url.Description}: {url.Url}");
        break;
    case LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionAES aes:
        var decrypted = aes.Decrypt(preimage);
        Console.WriteLine(decrypted);
        break;
}
```

### LNURL-Verify (LUD-21)

```csharp
// Check if a payment has been settled
if (callbackResponse.VerifyUrl != null)
{
    var verifyResponse = await callbackResponse.FetchVerifyResponse(httpClient);

    if (verifyResponse.Settled)
        Console.WriteLine($"Payment settled! Preimage: {verifyResponse.Preimage}");
    else
        Console.WriteLine("Payment not yet settled.");
}

// Or use the static method directly
var status = await LNURLVerifyResponse.FetchStatus(
    new Uri("https://example.com/verify/894e7f7e"), httpClient);
```

### LNURL-Withdraw (LUD-03)

```csharp
var withdrawRequest = (LNURLWithdrawRequest)await LNURL.LNURL.FetchInformation(uri, httpClient);

// Send a BOLT11 invoice to claim the withdrawal
var response = await withdrawRequest.SendRequest(
    bolt11: "lnbc10n1...",
    httpClient: httpClient);

Console.WriteLine($"Status: {response.Status}");

// With PIN support (BoltCard)
var responseWithPin = await withdrawRequest.SendRequest(
    bolt11: "lnbc10n1...",
    httpClient: httpClient,
    pin: "1234");
```

### LNURL-Auth (LUD-04)

```csharp
using NBitcoin;

var authRequest = (LNAuthRequest)await LNURL.LNURL.FetchInformation(lnurl, httpClient);

// Sign the challenge with a private key
var key = new Key();
var sig = authRequest.SignChallenge(key);

// Verify locally (optional)
bool valid = LNAuthRequest.VerifyChallenge(sig, key.PubKey,
    Encoders.Hex.DecodeData(authRequest.K1));

// Send the signed challenge to the service
var response = await authRequest.SendChallenge(key, httpClient);
```

### LNURL-Channel (LUD-02)

```csharp
var channelRequest = (LNURLChannelRequest)await LNURL.LNURL.FetchInformation(uri, httpClient);

// Request a channel
await channelRequest.SendRequest(
    ourId: ourNodePubKey,
    privateChannel: true,
    httpClient: httpClient);

// Cancel a channel request
await channelRequest.CancelRequest(ourId: ourNodePubKey, httpClient: httpClient);
```

### Encoding LNURLs

```csharp
// Encode a service URL to bech32 LNURL
var bech32 = LNURL.LNURL.EncodeBech32(new Uri("https://service.com/api?q=test"));
// Result: "lnurl1dp68gurn8ghj7..."

// Encode to a lightning: URI
var lightningUri = LNURL.LNURL.EncodeUri(
    new Uri("https://service.com/api?q=test"), tag: null, bech32: true);
// Result: "lightning:lnurl1dp68gurn8ghj7..."

// Encode to LUD-17 scheme
var lud17Uri = LNURL.LNURL.EncodeUri(
    new Uri("https://service.com/pay"), tag: "payRequest", bech32: false);
// Result: "lnurlp://service.com/pay"
```

### Payer Data (LUD-18)

```csharp
// Check what payer data the service accepts
if (payRequest.PayerData != null)
{
    Console.WriteLine($"Name required: {payRequest.PayerData.Name?.Mandatory}");
    Console.WriteLine($"Email required: {payRequest.PayerData.Email?.Mandatory}");
    Console.WriteLine($"Pubkey required: {payRequest.PayerData.Pubkey?.Mandatory}");
}

// Send payment with payer data
var payerData = new LNURLPayRequest.LUD18PayerDataResponse
{
    Name = "Alice",
    Email = "alice@example.com"
};

var response = await payRequest.SendRequest(
    amount: new LightMoney(10000),
    network: Network.Main,
    httpClient: httpClient,
    payerData: payerData);
```

### BoltCard (NFC Hardware Wallet)

```csharp
// Extract card info from an NFC tap URL
var result = BoltCardHelper.ExtractBoltCardFromRequest(
    new Uri("https://card.example.com?p=4E2E289D...&c=E19CCB1F..."),
    aesDecryptKey,
    out var error);

if (result != null)
{
    Console.WriteLine($"Card UID: {result.Value.uid}");
    Console.WriteLine($"Counter: {result.Value.counter}");

    // Verify CMAC
    bool cmacValid = BoltCardHelper.CheckCmac(
        result.Value.rawUid, result.Value.rawCtr, cmacKey, result.Value.c, out error);
}
```

## JSON Serialization

The library supports both **Newtonsoft.Json** and **System.Text.Json**.

### Newtonsoft.Json

Requires the `LNURL` package (not just `LNURL.Core`). All model classes have `[JsonProperty]` attributes that work when Newtonsoft.Json is present at runtime:

```csharp
using Newtonsoft.Json;

var payRequest = JsonConvert.DeserializeObject<LNURLPayRequest>(json);
var json = JsonConvert.SerializeObject(payRequest);
```

### System.Text.Json

Works with either `LNURL` or `LNURL.Core`. Use the pre-configured options from `LNURLJsonOptions`:

```csharp
using System.Text.Json;
using LNURL;

// Use the shared default options (singleton, optimal performance)
var payRequest = JsonSerializer.Deserialize<LNURLPayRequest>(json, LNURLJsonOptions.Default);
var json = JsonSerializer.Serialize(payRequest, LNURLJsonOptions.Default);

// Or add LNURL converters to your own options
var myOptions = new JsonSerializerOptions { /* your settings */ };
LNURLJsonOptions.AddConverters(myOptions);
```

The `LNURLJsonOptions.Default` instance includes converters for `Uri`, `LightMoney`, `NodeInfo`, `PubKey`, `ECDSASignature`, and polymorphic success action deserialization.

## Migrating from `LNURL` to `LNURL.Core`

If you want to drop the Newtonsoft.Json dependency:

1. Replace `dotnet add package LNURL` with `dotnet add package LNURL.Core`
2. Replace `JsonConvert.DeserializeObject<T>(json)` with `JsonSerializer.Deserialize<T>(json, LNURLJsonOptions.Default)`
3. Replace `JsonConvert.SerializeObject(obj)` with `JsonSerializer.Serialize(obj, LNURLJsonOptions.Default)`
4. The `AdditionalData` property (which used `IDictionary<string, JToken>`) has been removed from `LNURLPayRequest`

No other API changes are needed. All class names, property names, and method signatures are identical.

## API Documentation

Full API documentation is automatically generated from XML doc comments and published to **[GitHub Pages](https://kukks.github.io/LNURL/)**.

## Dependencies

### LNURL.Core
- [BTCPayServer.Lightning.Common](https://www.nuget.org/packages/BTCPayServer.Lightning.Common) — Lightning Network types

### LNURL (adds on top of Core)
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) — JSON serialization

## Requirements

- .NET 8.0 or later

## License

[MIT](LICENSE)
