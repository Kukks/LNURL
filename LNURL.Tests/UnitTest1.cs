using System;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Xunit;

namespace LNURL.Tests
{
    public class UnitTest1
    {

        [Fact]
        public void CanHandlePayLinkEdgeCase()
        {
            // from https://github.com/btcpayserver/btcpayserver/issues/4393
            var json = 
                
            "{"+
            "    \"callback\": \"https://coincorner.io/lnurl/withdrawreq/auth/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx?picc_data=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\","+
            "    \"defaultDescription\": \"CoinCorner Withdrawal ⚡️\","+
            "    \"maxWithdrawable\": 363003000,"+
            "    \"minWithdrawable\": 1000,"+
            "    \"k1\": \"xxxxxxxxxx\","+
            "    \"tag\": \"withdrawRequest\","+
            "    \"payLink\": \"\"" +
                "}";

            var req = JsonConvert.DeserializeObject<LNURLWithdrawRequest>(json);
            
            Assert.Null(req.PayLink);
            
            json = 
                
                "{"+
                "    \"callback\": \"https://coincorner.io/lnurl/withdrawreq/auth/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx?picc_data=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\","+
                "    \"defaultDescription\": \"CoinCorner Withdrawal ⚡️\","+
                "    \"maxWithdrawable\": 363003000,"+
                "    \"minWithdrawable\": 1000,"+
                "    \"k1\": \"xxxxxxxxxx\","+
                "    \"tag\": \"withdrawRequest\""+
                "}";

            req = JsonConvert.DeserializeObject<LNURLWithdrawRequest>(json);
            
            Assert.Null(req.PayLink);
        }
        
        [Theory]
        [InlineData("kukks@btcpay.kukks.org", "https://btcpay.kukks.org/.well-known/lnurlp/kukks")]
        [InlineData("kukks@tor.onion","http://tor.onion/.well-known/lnurlp/kukks")]
        public void CanParseLightningAddress(string lightningAddress, string expectedUrl)
        {
            Assert.Equal(expectedUrl, LNURL.ExtractUriFromInternetIdentifier(lightningAddress).ToString());
            
        }

        [Fact]
        public void CanEncodeDecodeLNUrl()
        {
            var uri = LNURL.Parse(
                "LNURL1DP68GURN8GHJ7UM9WFMXJCM99E3K7MF0V9CXJ0M385EKVCENXC6R2C35XVUKXEFCV5MKVV34X5EKZD3EV56NYD3HXQURZEPEXEJXXEPNXSCRVWFNV9NXZCN9XQ6XYEFHVGCXXCMYXYMNSERXFQ5FNS",
                out var tag);
            Assert.Null(tag);
            Assert.NotNull(uri);
            Assert.Equal("https://service.com/api?q=3fc3645b439ce8e7f2553a69e5267081d96dcd340693afabe04be7b0ccd178df",
                uri.ToString());
            Assert.Equal(
                "LNURL1DP68GURN8GHJ7UM9WFMXJCM99E3K7MF0V9CXJ0M385EKVCENXC6R2C35XVUKXEFCV5MKVV34X5EKZD3EV56NYD3HXQURZEPEXEJXXEPNXSCRVWFNV9NXZCN9XQ6XYEFHVGCXXCMYXYMNSERXFQ5FNS",
                LNURL.EncodeBech32(uri), StringComparer.InvariantCultureIgnoreCase);

            Assert.Equal("lightning:LNURL1DP68GURN8GHJ7UM9WFMXJCM99E3K7MF0V9CXJ0M385EKVCENXC6R2C35XVUKXEFCV5MKVV34X5EKZD3EV56NYD3HXQURZEPEXEJXXEPNXSCRVWFNV9NXZCN9XQ6XYEFHVGCXXCMYXYMNSERXFQ5FNS",
                LNURL.EncodeUri(uri, null, true).ToString(), StringComparer.InvariantCultureIgnoreCase);

            Assert.Throws<ArgumentNullException>(() =>
            {
                LNURL.EncodeUri(uri, null, false);
            });
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                LNURL.EncodeUri(uri, "swddwdd", false);
            });
            var payRequestUri = LNURL.EncodeUri(uri, "payRequest", false);
            Assert.Equal("lnurlp", payRequestUri.Scheme);
            
            
            uri = LNURL.Parse(
                "lnurlp://service.com/api?q=3fc3645b439ce8e7f2553a69e5267081d96dcd340693afabe04be7b0ccd178df",
                out tag);
            Assert.Equal("payRequest", tag);
            Assert.NotNull(uri);
            Assert.Equal("https://service.com/api?q=3fc3645b439ce8e7f2553a69e5267081d96dcd340693afabe04be7b0ccd178df",
                uri.ToString());
        }

        [Fact]
        public async Task CanUseLNURLAUTH()
        {

            Assert.Throws<ArgumentException>(() =>
            {
                LNURL.EncodeUri(new Uri("https://kukks.org"), "login", true);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                LNURL.EncodeUri(new Uri("https://kukks.org?tag=login"), "login", true);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                LNURL.EncodeUri(new Uri("https://kukks.org?tag=login&k1=123"), "login", true);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                var k1 = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
                LNURL.EncodeUri(new Uri($"https://kukks.org?tag=login&k1={k1}&action=xyz"), "login", true);
            });
            
            var k1 = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
           var lnurl = LNURL.EncodeUri(new Uri($"https://kukks.org?tag=login&k1={k1}"), "login", true);

           var request = Assert.IsType<LNAuthRequest>(await LNURL.FetchInformation(lnurl, null));

           var linkingKey = new Key();
           var sig = request.SignChallenge(linkingKey);
           Assert.True( LNAuthRequest.VerifyChallenge(sig, linkingKey.PubKey, Encoders.Hex.DecodeData(k1)));

        }

        [Fact]
        public async Task payerDataSerializerTest()
        {
            var req =
                new LNURLPayRequest()
                {
                    PayerData = new LNURLPayRequest.LUD18PayerData()
                    {
                        Auth = new LNURLPayRequest.AuthPayerDataField()
                        {
                            K1 = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)),
                            Mandatory = false,
                        },
                        Pubkey = new LNURLPayRequest.PayerDataField()
                        {
                            Mandatory = true
                        },
                        Name = new LNURLPayRequest.PayerDataField()
                        {
                        }
                    }
                };

           req =  JsonConvert.DeserializeObject<LNURLPayRequest>(JsonConvert.SerializeObject(req));
           Assert.NotNull(req.PayerData);
           Assert.True(req.PayerData.Pubkey.Mandatory);
           Assert.False(req.PayerData.Name.Mandatory);
           Assert.False(req.PayerData.Auth.Mandatory);
           Assert.Null(req.PayerData.Email);

           var k = new Key();

           var resp = new LNURLPayRequest.LUD18PayerDataResponse()
           {
               Auth = new LNURLPayRequest.LUD18AuthPayerDataResponse()
               {
                   K1 = req.PayerData.Auth.K1,
                   Key = k.PubKey,
                   Sig = k.Sign(new uint256(Encoders.Hex.DecodeData(req.PayerData.Auth.K1)))
               },
           };

           resp =  JsonConvert.DeserializeObject<LNURLPayRequest.LUD18PayerDataResponse>(JsonConvert.SerializeObject(resp));
           Assert.False(req.VerifyPayerData(resp));
           resp.Pubkey = k.PubKey;
           resp =  JsonConvert.DeserializeObject<LNURLPayRequest.LUD18PayerDataResponse>(JsonConvert.SerializeObject(resp));
           Assert.True(req.VerifyPayerData(resp));
           resp.Email = "test@test.com";
           Assert.False(req.VerifyPayerData(resp));

           resp.Email = null;
           resp.Name = "sdasds";
           Assert.True(req.VerifyPayerData(resp));
        }

[Fact]
        public async Task CanUseBoltCardHelper()
        {
            var key = Convert.FromHexString("0c3b25d92b38ae443229dd59ad34b85d");
            var cmacKey = Convert.FromHexString("b45775776cb224c75bcde7ca3704e933");
            var result = BoltCardHelper.ExtractBoltCardFromRequest(
                new Uri("https://test.com?p=4E2E289D945A66BB13377A728884E867&c=E19CCB1FED8892CE"),
                key, out var error);
            
            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Equal((uint)3, result.Value.counter);
            Assert.Equal("04996c6a926980", result.Value.uid);
            Assert.True(BoltCardHelper.CheckCmac(result.Value.rawUid, result.Value.rawCtr, cmacKey, result.Value.c,
                out error));

        }

    }
}
