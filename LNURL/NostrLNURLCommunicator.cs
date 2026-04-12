using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Secp256k1;
using NNostr.Client;
using NNostr.Client.Protocols;

namespace LNURL;

/// <summary>
/// An <see cref="ILNURLCommunicator"/> that performs LNURL requests over Nostr relays.
/// Supports both <c>naddr</c> (direct fetch of Kind 31120 replaceable events) and
/// <c>nprofile</c> (NIP-17 private DM exchange) addressing modes.
/// </summary>
public class NostrLNURLCommunicator : ILNURLCommunicator
{
    private readonly NostrClient _nostrClient;

    /// <summary>
    /// Initializes a new instance with an existing <see cref="NostrClient"/>.
    /// </summary>
    public NostrLNURLCommunicator(NostrClient nostrClient)
    {
        _nostrClient = nostrClient;
    }

    /// <summary>
    /// Initializes a new instance that connects to the specified relay URI.
    /// </summary>
    public NostrLNURLCommunicator(Uri relayUri)
    {
        _nostrClient = new NostrClient(relayUri);
    }

    /// <summary>
    /// The Nostr event kind for LNURL parameter events (parameterized replaceable).
    /// </summary>
    public const int LnurlParameterEventKind = 31120;

    /// <inheritdoc />
    public async Task<string> SendRequest(Uri lnurl, CancellationToken cancellationToken = default)
    {
        var note = lnurl.Host.FromNIP19Note();
        switch (note)
        {
            case NIP19.NostrAddressNote addressNote:
            {
                var client = _nostrClient ??
                             new NostrClient(new Uri(addressNote.Relays.First()));
                return await FetchReplaceable(client, addressNote, cancellationToken);
            }
            case NIP19.NosteProfileNote profileNote:
            {
                var client = _nostrClient ??
                             new NostrClient(new Uri(profileNote.Relays.First()));
                return await SendViaNip17(client, profileNote, lnurl.Query, cancellationToken);
            }
            default:
                throw new NotSupportedException(
                    "The nostr: URI must contain an naddr or nprofile.");
        }
    }

    private static async Task<string> FetchReplaceable(NostrClient nostrClient,
        NIP19.NostrAddressNote addressNote, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<string>();

        await nostrClient.CreateSubscription("lnurl-params",
            new[]
            {
                new NostrSubscriptionFilter
                {
                    Kinds = new[] { (int)addressNote.Kind },
                    Authors = new[] { addressNote.Author },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["#d"] = JsonSerializer.SerializeToElement(new[] { Encoding.UTF8.GetString(Convert.FromHexString(addressNote.Identifier)) })
                    }
                }
            }, cancellationToken);

        nostrClient.EventsReceived += (_, args) =>
        {
            foreach (var evt in args.events)
            {
                tcs.TrySetResult(evt.Content);
            }
        };

        nostrClient.EoseReceived += (_, _) =>
        {
            tcs.TrySetException(
                new LNUrlException("No LNURL parameter event found on relay."));
        };

        await nostrClient.ConnectAndWaitUntilConnected(cancellationToken, cancellationToken);
        _ = nostrClient.ListenForMessages();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        cts.Token.Register(() => tcs.TrySetCanceled(cts.Token));

        return await tcs.Task;
    }

    private static async Task<string> SendViaNip17(NostrClient nostrClient,
        NIP19.NosteProfileNote nostrProfileNote, string content, CancellationToken cancellationToken)
    {
        var tmpKey = ECPrivKey.Create(RandomUtils.GetBytes(32));
        var tmpPubKey = tmpKey.CreateXOnlyPubKey();
        var recipientPubKey = NostrExtensions.ParsePubKey(nostrProfileNote.PubKey);

        var dm = new NostrEvent
        {
            Content = content ?? string.Empty,
            Kind = 14,
            PublicKey = tmpPubKey.ToHex(),
            CreatedAt = DateTimeOffset.Now,
            Tags = new List<NostrEventTag>
            {
                new() { TagIdentifier = "p", Data = new List<string> { nostrProfileNote.PubKey } }
            }
        };

        var giftWrap = await NIP17.Create(dm, tmpKey, recipientPubKey, dm.Tags.ToArray());

        var tcs = new TaskCompletionSource<string>();

        await nostrClient.CreateSubscription("lnurl-response",
            new[]
            {
                new NostrSubscriptionFilter
                {
                    ReferencedPublicKeys = new[] { tmpPubKey.ToHex() },
                    Kinds = new[] { 1059 }
                }
            }, cancellationToken);

        nostrClient.EventsReceived += async (_, args) =>
        {
            foreach (var evt in args.events)
            {
                try
                {
                    var innerEvent = await NIP17.Open(evt, tmpKey);
                    tcs.TrySetResult(innerEvent.Content);
                }
                catch
                {
                }
            }
        };

        await nostrClient.ConnectAndWaitUntilConnected(cancellationToken, cancellationToken);
        _ = nostrClient.ListenForMessages();

        await nostrClient.PublishEvent(giftWrap, cancellationToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        cts.Token.Register(() => tcs.TrySetCanceled(cts.Token));

        return await tcs.Task;
    }
}
