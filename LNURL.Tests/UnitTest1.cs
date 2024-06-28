using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Altcoins.Elements;
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
                "{" +
                "    \"callback\": \"https://coincorner.io/lnurl/withdrawreq/auth/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx?picc_data=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\"," +
                "    \"defaultDescription\": \"CoinCorner Withdrawal ⚡️\"," +
                "    \"maxWithdrawable\": 363003000," +
                "    \"minWithdrawable\": 1000," +
                "    \"k1\": \"xxxxxxxxxx\"," +
                "    \"tag\": \"withdrawRequest\"," +
                "    \"payLink\": \"\"" +
                "}";

            var req = JsonConvert.DeserializeObject<LNURLWithdrawRequest>(json);

            Assert.Null(req.PayLink);

            json =
                "{" +
                "    \"callback\": \"https://coincorner.io/lnurl/withdrawreq/auth/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx?picc_data=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\"," +
                "    \"defaultDescription\": \"CoinCorner Withdrawal ⚡️\"," +
                "    \"maxWithdrawable\": 363003000," +
                "    \"minWithdrawable\": 1000," +
                "    \"k1\": \"xxxxxxxxxx\"," +
                "    \"tag\": \"withdrawRequest\"" +
                "}";

            req = JsonConvert.DeserializeObject<LNURLWithdrawRequest>(json);

            Assert.Null(req.PayLink);
        }

        [Theory]
        [InlineData("kukks@btcpay.kukks.org", "https://btcpay.kukks.org/.well-known/lnurlp/kukks")]
        [InlineData("kukks@btcpay.kukks.org:4000", "https://btcpay.kukks.org:4000/.well-known/lnurlp/kukks")]
        [InlineData("kukks@tor.onion","http://tor.onion/.well-known/lnurlp/kukks")]
        [InlineData("kukks@tor.onion:4000","http://tor.onion:4000/.well-known/lnurlp/kukks")]
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

            Assert.Equal(
                "lightning:LNURL1DP68GURN8GHJ7UM9WFMXJCM99E3K7MF0V9CXJ0M385EKVCENXC6R2C35XVUKXEFCV5MKVV34X5EKZD3EV56NYD3HXQURZEPEXEJXXEPNXSCRVWFNV9NXZCN9XQ6XYEFHVGCXXCMYXYMNSERXFQ5FNS",
                LNURL.EncodeUri(uri, null, true).ToString(), StringComparer.InvariantCultureIgnoreCase);

            Assert.Throws<ArgumentNullException>(() => { LNURL.EncodeUri(uri, null, false); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { LNURL.EncodeUri(uri, "swddwdd", false); });
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
            Assert.Throws<ArgumentException>(() => { LNURL.EncodeUri(new Uri("https://kukks.org"), "login", true); });
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
            Assert.True(LNAuthRequest.VerifyChallenge(sig, linkingKey.PubKey, Encoders.Hex.DecodeData(k1)));
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

            req = JsonConvert.DeserializeObject<LNURLPayRequest>(JsonConvert.SerializeObject(req));
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

            resp = JsonConvert.DeserializeObject<LNURLPayRequest.LUD18PayerDataResponse>(
                JsonConvert.SerializeObject(resp));
            Assert.False(req.VerifyPayerData(resp));
            resp.Pubkey = k.PubKey;
            resp = JsonConvert.DeserializeObject<LNURLPayRequest.LUD18PayerDataResponse>(
                JsonConvert.SerializeObject(resp));
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
            Assert.Equal((uint) 3, result.Value.counter);
            Assert.Equal("04996c6a926980", result.Value.uid);
            Assert.True(BoltCardHelper.CheckCmac(result.Value.rawUid, result.Value.rawCtr, cmacKey, result.Value.c,
                out error));

            var manualP = BoltCardHelper.CreatePValue(key, result.Value.counter, result.Value.uid);
            var manualPResult = BoltCardHelper.ExtractUidAndCounterFromP(manualP, key, out error);
            Assert.Null(error);
            Assert.NotNull(manualPResult);
            Assert.Equal((uint) 3, manualPResult.Value.counter);
            Assert.Equal("04996c6a926980", manualPResult.Value.uid);

            var manualC = BoltCardHelper.CreateCValue(result.Value.rawUid, result.Value.rawCtr, cmacKey);
            Assert.Equal(result.Value.c, manualC);
        }

        [Fact]
        public async Task DeterministicCards()
        {
            var masterSeed = RandomUtils.GetBytes(64);
            var masterSeedSlip21 = Slip21Node.FromSeed(masterSeed);

            var i = Random.Shared.Next(0, 10000);
            var k1 = masterSeedSlip21.DeriveChild(i + "k1").Key.ToBytes().Take(16).ToArray();
            var k2 = masterSeedSlip21.DeriveChild(i + "k2").Key.ToBytes().Take(16).ToArray();

            var counter = (uint) Random.Shared.Next(0, 1000);
            var uid = Convert.ToHexString(RandomUtils.GetBytes(7));
            var pParam = Convert.ToHexString(BoltCardHelper.CreatePValue(k1, counter, uid));
            var cParam = Convert.ToHexString(BoltCardHelper.CreateCValue(uid, counter, k2));
            var lnurlw = $"https://test.com?p={pParam}&c={cParam}";

            var result = BoltCardHelper.ExtractBoltCardFromRequest(new Uri(lnurlw), k1, out var error);
            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Equal(uid.ToLowerInvariant(), result.Value.uid.ToLowerInvariant());
            Assert.Equal(counter, result.Value.counter);
            Assert.True(BoltCardHelper.CheckCmac(result.Value.rawUid, result.Value.rawCtr, k2, result.Value.c,
                out error));
            Assert.Null(error);


            for (int j = 0; j <= 10000; j++)
            {
                var brutek1 = masterSeedSlip21.DeriveChild(j + "k1").Key.ToBytes().Take(16).ToArray();
                var brutek2 = masterSeedSlip21.DeriveChild(j + "k2").Key.ToBytes().Take(16).ToArray();
                try
                {
                    var bruteResult = BoltCardHelper.ExtractBoltCardFromRequest(new Uri(lnurlw), brutek1, out error);
                    Assert.Null(error);
                    Assert.NotNull(bruteResult);
                    Assert.Equal(uid.ToLowerInvariant(), bruteResult.Value.uid.ToLowerInvariant());
                    Assert.Equal(counter, bruteResult.Value.counter);
                    Assert.True(BoltCardHelper.CheckCmac(bruteResult.Value.rawUid, bruteResult.Value.rawCtr, brutek2,
                        bruteResult.Value.c, out error));
                    Assert.Null(error);

                    break;
                }
                catch (Exception e)
                {
                }
            }
        }
        //from https://github.com/boltcard/boltcard/blob/7745c9f20d5ad0129cb4b3fc534441038e79f5e6/docs/TEST_VECTORS.md
        [Theory]
        [InlineData("4E2E289D945A66BB13377A728884E867", "E19CCB1FED8892CE", "04996c6a926980", 3)]
        [InlineData("00F48C4F8E386DED06BCDC78FA92E2FE", "66B4826EA4C155B4", "04996c6a926980", 5)]
        [InlineData("0DBF3C59B59B0638D60B5842A997D4D1", "CC61660C020B4D96", "04996c6a926980", 7)]
        public void TestDecryptAndValidate(string pValueHex, string cValueHex, string expectedUidHex, uint expectedCtr)
        {
                
            var aesDecryptKey = Convert.FromHexString("0c3b25d92b38ae443229dd59ad34b85d");
            var aesCmacKey = Convert.FromHexString("b45775776cb224c75bcde7ca3704e933");
            byte[] pValue = Convert.FromHexString(pValueHex);
            byte[] cValue = Convert.FromHexString(cValueHex);

            // Decrypt p value
            var res = BoltCardHelper.ExtractUidAndCounterFromP(pValue, aesDecryptKey, out _);

            // Check UID and counter
            Assert.Equal(expectedUidHex, res.Value.uid);
            Assert.Equal(expectedCtr, res.Value.counter);

            // Validate CMAC
            var cmacIsValid = BoltCardHelper.CheckCmac(res.Value.rawUid, res.Value.rawCtr, aesCmacKey, cValue, out _);
            Assert.True(cmacIsValid, "CMAC validation failed");
        }

    }
}