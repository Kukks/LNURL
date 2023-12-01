using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Secp256k1;
using Newtonsoft.Json.Linq;
using NNostr.Client;
using NNostr.Client.Protocols;

namespace LNURL;

public class NostrLNURLCommunicator: ILNURLCommunicator
{
    private readonly NostrClient? _nostrClient;

    public NostrLNURLCommunicator(NostrClient nostrClient)
    {
        _nostrClient = nostrClient;
    }

    public NostrLNURLCommunicator(Uri relayUri)
    {
        _nostrClient = new NostrClient(relayUri);
    }
    
    public async Task<JObject> SendRequest(Uri lnurl, CancellationToken cancellationToken = default)
    {
        if (lnurl.Host.FromNIP19Note() is NIP19.NosteProfileNote nosteProfileNote)
        {
            
            var  evt = await GetLNURLNostrEventThroughNip4(_nostrClient?? new NostrClient(new Uri(nosteProfileNote.Relays.First())),nosteProfileNote,lnurl.Query, cancellationToken);
            return JObject.Parse(evt);
        };
        
        throw new NotSupportedException("the nostr Uri needs to be an nprofile");
    }
    private static async Task<string> GetLNURLNostrEventThroughNip4(NostrClient nostrClient,
        NIP19.NosteProfileNote nostrProfileNote, string content, CancellationToken cancellationToken)
    {
        var tmpKey = ECPrivKey.Create(RandomUtils.GetBytes(32));
        var lnurlRequestEvent = new NostrEvent()
        {
            Content = content,
            Kind = 4,
            PublicKey = tmpKey.CreateXOnlyPubKey().ToHex(),
            CreatedAt = DateTimeOffset.Now,
            Tags = new List<NostrEventTag>()
            {
                new()
                {
                    TagIdentifier = "p",
                    Data = new List<string>()
                    {
                        nostrProfileNote.PubKey
                    }
                }
            }
        };
        await lnurlRequestEvent.ComputeIdAndSignAsync(tmpKey);

        var filterId = "event-filter-" +Guid.NewGuid();

        var tcs = new TaskCompletionSource<string>(cancellationToken);
        nostrClient.EventsReceived += async (_, args) =>
        {
            if (args.subscriptionId != filterId) return;
            var matchedEvent =
                args.events.FirstOrDefault(evt =>
                    evt.PublicKey == nostrProfileNote.PubKey &&
                    evt.GetTaggedData("p").Any(s => s == lnurlRequestEvent.PublicKey)
                );
            if (matchedEvent != null)
            {
                tcs.SetResult(await matchedEvent.DecryptNip04EventAsync(tmpKey));
            }
        };
        var filter = new NostrSubscriptionFilter()
        {
            Authors = new[] {nostrProfileNote.PubKey},
            ReferencedPublicKeys = new[] {lnurlRequestEvent.PublicKey}
        };
        await nostrClient.ConnectAndWaitUntilConnected(cancellationToken);
        await nostrClient.CreateSubscription(filterId, new[] {filter}, cancellationToken);
        await nostrClient.PublishEvent(lnurlRequestEvent, cancellationToken);
        _ = nostrClient.ListenForMessages();
        try
        {

            return await tcs.Task;
        }
        catch
        {
            return null;
        }
        finally
        {
            await nostrClient.CloseSubscription(filterId, cancellationToken);
        }
    }
}