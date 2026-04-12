using System.Runtime.CompilerServices;
using LNURL;
using LNURL.JsonConverters;
using LNURL.JsonConverters.SystemTextJson;

[assembly: TypeForwardedTo(typeof(LNURL.LNURL))]
[assembly: TypeForwardedTo(typeof(LNURLPayRequest))]
[assembly: TypeForwardedTo(typeof(LNURLWithdrawRequest))]
[assembly: TypeForwardedTo(typeof(LNURLChannelRequest))]
[assembly: TypeForwardedTo(typeof(LNURLHostedChannelRequest))]
[assembly: TypeForwardedTo(typeof(LNAuthRequest))]
[assembly: TypeForwardedTo(typeof(LNUrlStatusResponse))]
[assembly: TypeForwardedTo(typeof(LNURLVerifyResponse))]
[assembly: TypeForwardedTo(typeof(LNURLJsonOptions))]
[assembly: TypeForwardedTo(typeof(LNUrlException))]
[assembly: TypeForwardedTo(typeof(Extensions))]
[assembly: TypeForwardedTo(typeof(BoltCardHelper))]
[assembly: TypeForwardedTo(typeof(ILNURLCommunicator))]
[assembly: TypeForwardedTo(typeof(HttpLNURLCommunicator))]

// JsonConverters (Newtonsoft)
[assembly: TypeForwardedTo(typeof(UriJsonConverter))]
[assembly: TypeForwardedTo(typeof(NodeUriJsonConverter))]
[assembly: TypeForwardedTo(typeof(PubKeyJsonConverter))]
[assembly: TypeForwardedTo(typeof(SigJsonConverter))]

// JsonConverters (System.Text.Json)
[assembly: TypeForwardedTo(typeof(STJUriJsonConverter))]
[assembly: TypeForwardedTo(typeof(STJLightMoneyJsonConverter))]
[assembly: TypeForwardedTo(typeof(STJNodeUriJsonConverter))]
[assembly: TypeForwardedTo(typeof(STJPubKeyJsonConverter))]
[assembly: TypeForwardedTo(typeof(STJSigJsonConverter))]
[assembly: TypeForwardedTo(typeof(STJSuccessActionJsonConverter))]
