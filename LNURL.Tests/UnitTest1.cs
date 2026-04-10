using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using NBitcoin;
using NBitcoin.Altcoins.Elements;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace LNURL.Tests
{
    public class UnitTest1
    {
        private static readonly JsonSerializerOptions StjOptions = LNURLJsonOptions.Default;

        #region Withdraw Request Tests

        [Fact]
        public void CanHandlePayLinkEdgeCase()
        {
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

        [Fact]
        public void CanHandlePayLinkEdgeCase_STJ()
        {
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

            var req = JsonSerializer.Deserialize<LNURLWithdrawRequest>(json, StjOptions);
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

            req = JsonSerializer.Deserialize<LNURLWithdrawRequest>(json, StjOptions);
            Assert.Null(req.PayLink);
        }

        [Fact]
        public void CanDeserializeWithdrawRequest_Newtonsoft()
        {
            var json = "{" +
                       "\"callback\":\"https://example.com/withdraw\"," +
                       "\"k1\":\"abc123\"," +
                       "\"tag\":\"withdrawRequest\"," +
                       "\"defaultDescription\":\"test withdrawal\"," +
                       "\"minWithdrawable\":1000," +
                       "\"maxWithdrawable\":50000000," +
                       "\"currentBalance\":25000000," +
                       "\"balanceCheck\":\"https://example.com/balance\"," +
                       "\"pinLimit\":10000000" +
                       "}";

            var req = JsonConvert.DeserializeObject<LNURLWithdrawRequest>(json);
            Assert.Equal("https://example.com/withdraw", req.Callback.ToString());
            Assert.Equal("abc123", req.K1);
            Assert.Equal("withdrawRequest", req.Tag);
            Assert.Equal("test withdrawal", req.DefaultDescription);
            Assert.Equal(new LightMoney(1000), req.MinWithdrawable);
            Assert.Equal(new LightMoney(50000000), req.MaxWithdrawable);
            Assert.Equal(new LightMoney(25000000), req.CurrentBalance);
            Assert.Equal("https://example.com/balance", req.BalanceCheck.ToString());
            Assert.Equal(new LightMoney(10000000), req.PinLimit);
        }

        [Fact]
        public void CanDeserializeWithdrawRequest_STJ()
        {
            var json = "{" +
                       "\"callback\":\"https://example.com/withdraw\"," +
                       "\"k1\":\"abc123\"," +
                       "\"tag\":\"withdrawRequest\"," +
                       "\"defaultDescription\":\"test withdrawal\"," +
                       "\"minWithdrawable\":1000," +
                       "\"maxWithdrawable\":50000000," +
                       "\"currentBalance\":25000000," +
                       "\"balanceCheck\":\"https://example.com/balance\"," +
                       "\"pinLimit\":10000000" +
                       "}";

            var req = JsonSerializer.Deserialize<LNURLWithdrawRequest>(json, StjOptions);
            Assert.Equal("https://example.com/withdraw", req.Callback.ToString());
            Assert.Equal("abc123", req.K1);
            Assert.Equal("withdrawRequest", req.Tag);
            Assert.Equal("test withdrawal", req.DefaultDescription);
            Assert.Equal(new LightMoney(1000), req.MinWithdrawable);
            Assert.Equal(new LightMoney(50000000), req.MaxWithdrawable);
            Assert.Equal(new LightMoney(25000000), req.CurrentBalance);
            Assert.Equal("https://example.com/balance", req.BalanceCheck.ToString());
            Assert.Equal(new LightMoney(10000000), req.PinLimit);
        }

        [Fact]
        public void WithdrawRequestRoundTrip_Newtonsoft()
        {
            var original = new LNURLWithdrawRequest
            {
                Callback = new Uri("https://example.com/withdraw"),
                K1 = "abc123",
                Tag = "withdrawRequest",
                DefaultDescription = "test",
                MinWithdrawable = new LightMoney(1000),
                MaxWithdrawable = new LightMoney(50000000),
                PinLimit = new LightMoney(5000000)
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<LNURLWithdrawRequest>(json);

            Assert.Equal(original.Callback, deserialized.Callback);
            Assert.Equal(original.K1, deserialized.K1);
            Assert.Equal(original.MinWithdrawable, deserialized.MinWithdrawable);
            Assert.Equal(original.MaxWithdrawable, deserialized.MaxWithdrawable);
            Assert.Equal(original.PinLimit, deserialized.PinLimit);
        }

        [Fact]
        public void WithdrawRequestRoundTrip_STJ()
        {
            var original = new LNURLWithdrawRequest
            {
                Callback = new Uri("https://example.com/withdraw"),
                K1 = "abc123",
                Tag = "withdrawRequest",
                DefaultDescription = "test",
                MinWithdrawable = new LightMoney(1000),
                MaxWithdrawable = new LightMoney(50000000),
                PinLimit = new LightMoney(5000000)
            };

            var json = JsonSerializer.Serialize(original, StjOptions);
            var deserialized = JsonSerializer.Deserialize<LNURLWithdrawRequest>(json, StjOptions);

            Assert.Equal(original.Callback, deserialized.Callback);
            Assert.Equal(original.K1, deserialized.K1);
            Assert.Equal(original.MinWithdrawable, deserialized.MinWithdrawable);
            Assert.Equal(original.MaxWithdrawable, deserialized.MaxWithdrawable);
            Assert.Equal(original.PinLimit, deserialized.PinLimit);
        }

        #endregion

        #region Pay Request Tests

        [Fact]
        public void CanDeserializePayRequest_Newtonsoft()
        {
            var json = "{" +
                       "\"callback\":\"https://example.com/pay\"," +
                       "\"metadata\":\"[[\\\"text/plain\\\",\\\"test\\\"]]\"," +
                       "\"tag\":\"payRequest\"," +
                       "\"minSendable\":1000," +
                       "\"maxSendable\":100000000," +
                       "\"commentAllowed\":144," +
                       "\"allowsNostr\":true," +
                       "\"nostrPubkey\":\"abc123\"" +
                       "}";

            var req = JsonConvert.DeserializeObject<LNURLPayRequest>(json);
            Assert.Equal("https://example.com/pay", req.Callback.ToString());
            Assert.Equal("payRequest", req.Tag);
            Assert.Equal(new LightMoney(1000), req.MinSendable);
            Assert.Equal(new LightMoney(100000000), req.MaxSendable);
            Assert.Equal(144, req.CommentAllowed);
            Assert.True(req.AllowsNostr);
            Assert.Equal("abc123", req.NostrPubkey);
            Assert.Single(req.ParsedMetadata);
            Assert.Equal("text/plain", req.ParsedMetadata[0].Key);
        }

        [Fact]
        public void CanDeserializePayRequest_STJ()
        {
            var json = "{" +
                       "\"callback\":\"https://example.com/pay\"," +
                       "\"metadata\":\"[[\\\"text/plain\\\",\\\"test\\\"]]\"," +
                       "\"tag\":\"payRequest\"," +
                       "\"minSendable\":1000," +
                       "\"maxSendable\":100000000," +
                       "\"commentAllowed\":144," +
                       "\"allowsNostr\":true," +
                       "\"nostrPubkey\":\"abc123\"" +
                       "}";

            var req = JsonSerializer.Deserialize<LNURLPayRequest>(json, StjOptions);
            Assert.Equal("https://example.com/pay", req.Callback.ToString());
            Assert.Equal("payRequest", req.Tag);
            Assert.Equal(new LightMoney(1000), req.MinSendable);
            Assert.Equal(new LightMoney(100000000), req.MaxSendable);
            Assert.Equal(144, req.CommentAllowed);
            Assert.True(req.AllowsNostr);
            Assert.Equal("abc123", req.NostrPubkey);
        }

        [Fact]
        public void PayRequestRoundTrip_Newtonsoft()
        {
            var original = new LNURLPayRequest
            {
                Callback = new Uri("https://example.com/pay"),
                Metadata = "[[\"text/plain\",\"test\"]]",
                Tag = "payRequest",
                MinSendable = new LightMoney(1000),
                MaxSendable = new LightMoney(100000000),
                CommentAllowed = 144,
                AllowsNostr = true,
                NostrPubkey = "deadbeef"
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<LNURLPayRequest>(json);

            Assert.Equal(original.Callback, deserialized.Callback);
            Assert.Equal(original.Tag, deserialized.Tag);
            Assert.Equal(original.MinSendable, deserialized.MinSendable);
            Assert.Equal(original.MaxSendable, deserialized.MaxSendable);
            Assert.Equal(original.CommentAllowed, deserialized.CommentAllowed);
            Assert.Equal(original.AllowsNostr, deserialized.AllowsNostr);
            Assert.Equal(original.NostrPubkey, deserialized.NostrPubkey);
        }

        [Fact]
        public void PayRequestRoundTrip_STJ()
        {
            var original = new LNURLPayRequest
            {
                Callback = new Uri("https://example.com/pay"),
                Metadata = "[[\"text/plain\",\"test\"]]",
                Tag = "payRequest",
                MinSendable = new LightMoney(1000),
                MaxSendable = new LightMoney(100000000),
                CommentAllowed = 144,
                AllowsNostr = true,
                NostrPubkey = "deadbeef"
            };

            var json = JsonSerializer.Serialize(original, StjOptions);
            var deserialized = JsonSerializer.Deserialize<LNURLPayRequest>(json, StjOptions);

            Assert.Equal(original.Callback, deserialized.Callback);
            Assert.Equal(original.Tag, deserialized.Tag);
            Assert.Equal(original.MinSendable, deserialized.MinSendable);
            Assert.Equal(original.MaxSendable, deserialized.MaxSendable);
            Assert.Equal(original.CommentAllowed, deserialized.CommentAllowed);
            Assert.Equal(original.AllowsNostr, deserialized.AllowsNostr);
            Assert.Equal(original.NostrPubkey, deserialized.NostrPubkey);
        }

        #endregion

        #region Callback Response + LUD-21 Verify Tests

        [Fact]
        public void CanDeserializeCallbackResponseWithVerify_Newtonsoft()
        {
            var json = "{" +
                       "\"pr\":\"lnbc10n1...\","+
                       "\"routes\":[]," +
                       "\"verify\":\"https://example.com/verify/894e7f7e\"," +
                       "\"disposable\":true" +
                       "}";

            var resp = JsonConvert.DeserializeObject<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json);
            Assert.Equal("lnbc10n1...", resp.Pr);
            Assert.NotNull(resp.VerifyUrl);
            Assert.Equal("https://example.com/verify/894e7f7e", resp.VerifyUrl.ToString());
            Assert.True(resp.Disposable);
            Assert.Empty(resp.Routes);
        }

        [Fact]
        public void CanDeserializeCallbackResponseWithVerify_STJ()
        {
            var json = "{" +
                       "\"pr\":\"lnbc10n1...\","+
                       "\"routes\":[]," +
                       "\"verify\":\"https://example.com/verify/894e7f7e\"," +
                       "\"disposable\":true" +
                       "}";

            var resp = JsonSerializer.Deserialize<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json, StjOptions);
            Assert.Equal("lnbc10n1...", resp.Pr);
            Assert.NotNull(resp.VerifyUrl);
            Assert.Equal("https://example.com/verify/894e7f7e", resp.VerifyUrl.ToString());
            Assert.True(resp.Disposable);
            Assert.Empty(resp.Routes);
        }

        [Fact]
        public void CallbackResponseWithoutVerify_Newtonsoft()
        {
            var json = "{\"pr\":\"lnbc10n1...\",\"routes\":[]}";
            var resp = JsonConvert.DeserializeObject<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json);
            Assert.Null(resp.VerifyUrl);
        }

        [Fact]
        public void CallbackResponseWithoutVerify_STJ()
        {
            var json = "{\"pr\":\"lnbc10n1...\",\"routes\":[]}";
            var resp = JsonSerializer.Deserialize<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json, StjOptions);
            Assert.Null(resp.VerifyUrl);
        }

        [Fact]
        public void CallbackResponseRoundTrip_Newtonsoft()
        {
            var original = new LNURLPayRequest.LNURLPayRequestCallbackResponse
            {
                Pr = "lnbc10n1...",
                VerifyUrl = new Uri("https://example.com/verify/test123"),
                Disposable = true
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json);

            Assert.Equal(original.Pr, deserialized.Pr);
            Assert.Equal(original.VerifyUrl, deserialized.VerifyUrl);
            Assert.Equal(original.Disposable, deserialized.Disposable);
        }

        [Fact]
        public void CallbackResponseRoundTrip_STJ()
        {
            var original = new LNURLPayRequest.LNURLPayRequestCallbackResponse
            {
                Pr = "lnbc10n1...",
                VerifyUrl = new Uri("https://example.com/verify/test123"),
                Disposable = true
            };

            var json = JsonSerializer.Serialize(original, StjOptions);
            var deserialized = JsonSerializer.Deserialize<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json, StjOptions);

            Assert.Equal(original.Pr, deserialized.Pr);
            Assert.Equal(original.VerifyUrl, deserialized.VerifyUrl);
            Assert.Equal(original.Disposable, deserialized.Disposable);
        }

        [Fact]
        public void CanDeserializeVerifyResponse_Settled_Newtonsoft()
        {
            var json = "{\"status\":\"OK\",\"settled\":true,\"preimage\":\"abcdef123456\",\"pr\":\"lnbc10n1...\"}";
            var resp = JsonConvert.DeserializeObject<LNURLVerifyResponse>(json);
            Assert.Equal("OK", resp.Status);
            Assert.True(resp.Settled);
            Assert.Equal("abcdef123456", resp.Preimage);
            Assert.Equal("lnbc10n1...", resp.Pr);
        }

        [Fact]
        public void CanDeserializeVerifyResponse_Settled_STJ()
        {
            var json = "{\"status\":\"OK\",\"settled\":true,\"preimage\":\"abcdef123456\",\"pr\":\"lnbc10n1...\"}";
            var resp = JsonSerializer.Deserialize<LNURLVerifyResponse>(json, StjOptions);
            Assert.Equal("OK", resp.Status);
            Assert.True(resp.Settled);
            Assert.Equal("abcdef123456", resp.Preimage);
            Assert.Equal("lnbc10n1...", resp.Pr);
        }

        [Fact]
        public void CanDeserializeVerifyResponse_Unsettled_Newtonsoft()
        {
            var json = "{\"status\":\"OK\",\"settled\":false,\"preimage\":null,\"pr\":\"lnbc10n1...\"}";
            var resp = JsonConvert.DeserializeObject<LNURLVerifyResponse>(json);
            Assert.False(resp.Settled);
            Assert.Null(resp.Preimage);
        }

        [Fact]
        public void CanDeserializeVerifyResponse_Unsettled_STJ()
        {
            var json = "{\"status\":\"OK\",\"settled\":false,\"preimage\":null,\"pr\":\"lnbc10n1...\"}";
            var resp = JsonSerializer.Deserialize<LNURLVerifyResponse>(json, StjOptions);
            Assert.False(resp.Settled);
            Assert.Null(resp.Preimage);
        }

        [Fact]
        public void VerifyResponseRoundTrip_Newtonsoft()
        {
            var original = new LNURLVerifyResponse
            {
                Status = "OK",
                Settled = true,
                Preimage = "abcdef",
                Pr = "lnbc10n1..."
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<LNURLVerifyResponse>(json);

            Assert.Equal(original.Settled, deserialized.Settled);
            Assert.Equal(original.Preimage, deserialized.Preimage);
            Assert.Equal(original.Pr, deserialized.Pr);
        }

        [Fact]
        public void VerifyResponseRoundTrip_STJ()
        {
            var original = new LNURLVerifyResponse
            {
                Status = "OK",
                Settled = true,
                Preimage = "abcdef",
                Pr = "lnbc10n1..."
            };

            var json = JsonSerializer.Serialize(original, StjOptions);
            var deserialized = JsonSerializer.Deserialize<LNURLVerifyResponse>(json, StjOptions);

            Assert.Equal(original.Settled, deserialized.Settled);
            Assert.Equal(original.Preimage, deserialized.Preimage);
            Assert.Equal(original.Pr, deserialized.Pr);
        }

        #endregion

        #region Success Action Tests

        [Fact]
        public void CanDeserializeSuccessAction_Message_Newtonsoft()
        {
            var json = "{\"pr\":\"lnbc10n1...\",\"routes\":[],\"successAction\":{\"tag\":\"message\",\"message\":\"Payment received!\"}}";
            var resp = JsonConvert.DeserializeObject<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json);
            var action = Assert.IsType<LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionMessage>(resp.SuccessAction);
            Assert.Equal("message", action.Tag);
            Assert.Equal("Payment received!", action.Message);
        }

        [Fact]
        public void CanDeserializeSuccessAction_Message_STJ()
        {
            var json = "{\"pr\":\"lnbc10n1...\",\"routes\":[],\"successAction\":{\"tag\":\"message\",\"message\":\"Payment received!\"}}";
            var resp = JsonSerializer.Deserialize<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json, StjOptions);
            var action = Assert.IsType<LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionMessage>(resp.SuccessAction);
            Assert.Equal("message", action.Tag);
            Assert.Equal("Payment received!", action.Message);
        }

        [Fact]
        public void CanDeserializeSuccessAction_Url_Newtonsoft()
        {
            var json = "{\"pr\":\"lnbc10n1...\",\"routes\":[],\"successAction\":{\"tag\":\"url\",\"description\":\"See result\",\"url\":\"https://example.com/result\"}}";
            var resp = JsonConvert.DeserializeObject<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json);
            var action = Assert.IsType<LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionUrl>(resp.SuccessAction);
            Assert.Equal("url", action.Tag);
            Assert.Equal("See result", action.Description);
            Assert.Equal("https://example.com/result", action.Url);
        }

        [Fact]
        public void CanDeserializeSuccessAction_Url_STJ()
        {
            var json = "{\"pr\":\"lnbc10n1...\",\"routes\":[],\"successAction\":{\"tag\":\"url\",\"description\":\"See result\",\"url\":\"https://example.com/result\"}}";
            var resp = JsonSerializer.Deserialize<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json, StjOptions);
            var action = Assert.IsType<LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionUrl>(resp.SuccessAction);
            Assert.Equal("url", action.Tag);
            Assert.Equal("See result", action.Description);
            Assert.Equal("https://example.com/result", action.Url);
        }

        [Fact]
        public void CanDeserializeSuccessAction_Aes_Newtonsoft()
        {
            var json = "{\"pr\":\"lnbc10n1...\",\"routes\":[],\"successAction\":{\"tag\":\"aes\",\"description\":\"Secret\",\"ciphertext\":\"dGVzdA==\",\"iv\":\"AAAAAAAAAAAAAAAAAAAAAA==\"}}";
            var resp = JsonConvert.DeserializeObject<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json);
            var action = Assert.IsType<LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionAES>(resp.SuccessAction);
            Assert.Equal("aes", action.Tag);
            Assert.Equal("Secret", action.Description);
            Assert.Equal("dGVzdA==", action.CipherText);
            Assert.Equal("AAAAAAAAAAAAAAAAAAAAAA==", action.IV);
        }

        [Fact]
        public void CanDeserializeSuccessAction_Aes_STJ()
        {
            var json = "{\"pr\":\"lnbc10n1...\",\"routes\":[],\"successAction\":{\"tag\":\"aes\",\"description\":\"Secret\",\"ciphertext\":\"dGVzdA==\",\"iv\":\"AAAAAAAAAAAAAAAAAAAAAA==\"}}";
            var resp = JsonSerializer.Deserialize<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json, StjOptions);
            var action = Assert.IsType<LNURLPayRequest.LNURLPayRequestCallbackResponse.LNURLPayRequestSuccessActionAES>(resp.SuccessAction);
            Assert.Equal("aes", action.Tag);
            Assert.Equal("Secret", action.Description);
            Assert.Equal("dGVzdA==", action.CipherText);
            Assert.Equal("AAAAAAAAAAAAAAAAAAAAAA==", action.IV);
        }

        [Fact]
        public void CallbackResponseWithNullSuccessAction_Newtonsoft()
        {
            var json = "{\"pr\":\"lnbc10n1...\",\"routes\":[],\"successAction\":null}";
            var resp = JsonConvert.DeserializeObject<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json);
            Assert.Null(resp.SuccessAction);
        }

        [Fact]
        public void CallbackResponseWithNullSuccessAction_STJ()
        {
            var json = "{\"pr\":\"lnbc10n1...\",\"routes\":[],\"successAction\":null}";
            var resp = JsonSerializer.Deserialize<LNURLPayRequest.LNURLPayRequestCallbackResponse>(json, StjOptions);
            Assert.Null(resp.SuccessAction);
        }

        #endregion

        #region Payer Data Tests

        [Fact]
        public void PayerDataSerializerTest_Newtonsoft()
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
        public void PayerDataSerializerTest_STJ()
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

            req = JsonSerializer.Deserialize<LNURLPayRequest>(
                JsonSerializer.Serialize(req, StjOptions), StjOptions);
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

            resp = JsonSerializer.Deserialize<LNURLPayRequest.LUD18PayerDataResponse>(
                JsonSerializer.Serialize(resp, StjOptions), StjOptions);
            Assert.False(req.VerifyPayerData(resp));
            resp.Pubkey = k.PubKey;
            resp = JsonSerializer.Deserialize<LNURLPayRequest.LUD18PayerDataResponse>(
                JsonSerializer.Serialize(resp, StjOptions), StjOptions);
            Assert.True(req.VerifyPayerData(resp));
            resp.Email = "test@test.com";
            Assert.False(req.VerifyPayerData(resp));

            resp.Email = null;
            resp.Name = "sdasds";
            Assert.True(req.VerifyPayerData(resp));
        }

        #endregion

        #region Status Response Tests

        [Fact]
        public void CanDeserializeErrorStatus_Newtonsoft()
        {
            var json = "{\"status\":\"ERROR\",\"reason\":\"Something went wrong\"}";
            var resp = JsonConvert.DeserializeObject<LNUrlStatusResponse>(json);
            Assert.Equal("ERROR", resp.Status);
            Assert.Equal("Something went wrong", resp.Reason);
        }

        [Fact]
        public void CanDeserializeErrorStatus_STJ()
        {
            var json = "{\"status\":\"ERROR\",\"reason\":\"Something went wrong\"}";
            var resp = JsonSerializer.Deserialize<LNUrlStatusResponse>(json, StjOptions);
            Assert.Equal("ERROR", resp.Status);
            Assert.Equal("Something went wrong", resp.Reason);
        }

        [Fact]
        public void StatusResponseRoundTrip_Newtonsoft()
        {
            var original = new LNUrlStatusResponse { Status = "OK", Reason = null };
            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<LNUrlStatusResponse>(json);
            Assert.Equal("OK", deserialized.Status);
        }

        [Fact]
        public void StatusResponseRoundTrip_STJ()
        {
            var original = new LNUrlStatusResponse { Status = "OK", Reason = null };
            var json = JsonSerializer.Serialize(original, StjOptions);
            var deserialized = JsonSerializer.Deserialize<LNUrlStatusResponse>(json, StjOptions);
            Assert.Equal("OK", deserialized.Status);
        }

        #endregion

        #region Channel Request Tests

        [Fact]
        public void CanDeserializeChannelRequest_Newtonsoft()
        {
            var json = "{\"uri\":\"03864ef025fde8fb587d989186ce6a4a186895ee44a926bfc370e2c366597a3f8f@3.33.236.230:9735\"," +
                       "\"callback\":\"https://example.com/channel\"," +
                       "\"k1\":\"abc123\"," +
                       "\"tag\":\"channelRequest\"}";

            var req = JsonConvert.DeserializeObject<LNURLChannelRequest>(json);
            Assert.NotNull(req.Uri);
            Assert.Equal("https://example.com/channel", req.Callback.ToString());
            Assert.Equal("abc123", req.K1);
            Assert.Equal("channelRequest", req.Tag);
        }

        [Fact]
        public void CanDeserializeChannelRequest_STJ()
        {
            var json = "{\"uri\":\"03864ef025fde8fb587d989186ce6a4a186895ee44a926bfc370e2c366597a3f8f@3.33.236.230:9735\"," +
                       "\"callback\":\"https://example.com/channel\"," +
                       "\"k1\":\"abc123\"," +
                       "\"tag\":\"channelRequest\"}";

            var req = JsonSerializer.Deserialize<LNURLChannelRequest>(json, StjOptions);
            Assert.NotNull(req.Uri);
            Assert.Equal("https://example.com/channel", req.Callback.ToString());
            Assert.Equal("abc123", req.K1);
            Assert.Equal("channelRequest", req.Tag);
        }

        #endregion

        #region Hosted Channel Request Tests

        [Fact]
        public void CanDeserializeHostedChannelRequest_Newtonsoft()
        {
            var json = "{\"uri\":\"03864ef025fde8fb587d989186ce6a4a186895ee44a926bfc370e2c366597a3f8f@3.33.236.230:9735\"," +
                       "\"alias\":\"TestNode\"," +
                       "\"k1\":\"abc123\"," +
                       "\"tag\":\"hostedChannelRequest\"}";

            var req = JsonConvert.DeserializeObject<LNURLHostedChannelRequest>(json);
            Assert.NotNull(req.Uri);
            Assert.Equal("TestNode", req.Alias);
            Assert.Equal("abc123", req.K1);
        }

        [Fact]
        public void CanDeserializeHostedChannelRequest_STJ()
        {
            var json = "{\"uri\":\"03864ef025fde8fb587d989186ce6a4a186895ee44a926bfc370e2c366597a3f8f@3.33.236.230:9735\"," +
                       "\"alias\":\"TestNode\"," +
                       "\"k1\":\"abc123\"," +
                       "\"tag\":\"hostedChannelRequest\"}";

            var req = JsonSerializer.Deserialize<LNURLHostedChannelRequest>(json, StjOptions);
            Assert.NotNull(req.Uri);
            Assert.Equal("TestNode", req.Alias);
            Assert.Equal("abc123", req.K1);
        }

        #endregion

        #region Cross-Serializer Compatibility Tests

        [Fact]
        public void NewtonsoftOutputCanBeReadBySTJ_WithdrawRequest()
        {
            var original = new LNURLWithdrawRequest
            {
                Callback = new Uri("https://example.com/withdraw"),
                K1 = "test123",
                Tag = "withdrawRequest",
                DefaultDescription = "cross-compat test",
                MinWithdrawable = new LightMoney(1000),
                MaxWithdrawable = new LightMoney(50000000)
            };

            var newtonsoftJson = JsonConvert.SerializeObject(original);
            var deserialized = JsonSerializer.Deserialize<LNURLWithdrawRequest>(newtonsoftJson, StjOptions);

            Assert.Equal(original.Callback, deserialized.Callback);
            Assert.Equal(original.K1, deserialized.K1);
            Assert.Equal(original.MinWithdrawable, deserialized.MinWithdrawable);
            Assert.Equal(original.MaxWithdrawable, deserialized.MaxWithdrawable);
        }

        [Fact]
        public void STJOutputCanBeReadByNewtonsoft_WithdrawRequest()
        {
            var original = new LNURLWithdrawRequest
            {
                Callback = new Uri("https://example.com/withdraw"),
                K1 = "test123",
                Tag = "withdrawRequest",
                DefaultDescription = "cross-compat test",
                MinWithdrawable = new LightMoney(1000),
                MaxWithdrawable = new LightMoney(50000000)
            };

            var stjJson = JsonSerializer.Serialize(original, StjOptions);
            var deserialized = JsonConvert.DeserializeObject<LNURLWithdrawRequest>(stjJson);

            Assert.Equal(original.Callback, deserialized.Callback);
            Assert.Equal(original.K1, deserialized.K1);
            Assert.Equal(original.MinWithdrawable, deserialized.MinWithdrawable);
            Assert.Equal(original.MaxWithdrawable, deserialized.MaxWithdrawable);
        }

        [Fact]
        public void NewtonsoftOutputCanBeReadBySTJ_PayRequest()
        {
            var original = new LNURLPayRequest
            {
                Callback = new Uri("https://example.com/pay"),
                Metadata = "[[\"text/plain\",\"test\"]]",
                Tag = "payRequest",
                MinSendable = new LightMoney(1000),
                MaxSendable = new LightMoney(100000000)
            };

            var newtonsoftJson = JsonConvert.SerializeObject(original);
            var deserialized = JsonSerializer.Deserialize<LNURLPayRequest>(newtonsoftJson, StjOptions);

            Assert.Equal(original.Callback, deserialized.Callback);
            Assert.Equal(original.MinSendable, deserialized.MinSendable);
            Assert.Equal(original.MaxSendable, deserialized.MaxSendable);
        }

        [Fact]
        public void NewtonsoftOutputCanBeReadBySTJ_VerifyResponse()
        {
            var original = new LNURLVerifyResponse
            {
                Status = "OK",
                Settled = true,
                Preimage = "abcdef123456",
                Pr = "lnbc10n1..."
            };

            var newtonsoftJson = JsonConvert.SerializeObject(original);
            var deserialized = JsonSerializer.Deserialize<LNURLVerifyResponse>(newtonsoftJson, StjOptions);

            Assert.Equal(original.Settled, deserialized.Settled);
            Assert.Equal(original.Preimage, deserialized.Preimage);
        }

        [Fact]
        public void NewtonsoftOutputCanBeReadBySTJ_PayerData()
        {
            var k = new Key();
            var k1 = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));

            var original = new LNURLPayRequest.LUD18PayerDataResponse
            {
                Name = "testuser",
                Pubkey = k.PubKey,
                Email = "test@example.com",
                Auth = new LNURLPayRequest.LUD18AuthPayerDataResponse
                {
                    K1 = k1,
                    Key = k.PubKey,
                    Sig = k.Sign(new uint256(Encoders.Hex.DecodeData(k1)))
                }
            };

            var newtonsoftJson = JsonConvert.SerializeObject(original);
            var deserialized = JsonSerializer.Deserialize<LNURLPayRequest.LUD18PayerDataResponse>(newtonsoftJson, StjOptions);

            Assert.Equal(original.Name, deserialized.Name);
            Assert.Equal(original.Pubkey, deserialized.Pubkey);
            Assert.Equal(original.Email, deserialized.Email);
            Assert.Equal(original.Auth.K1, deserialized.Auth.K1);
            Assert.Equal(original.Auth.Key, deserialized.Auth.Key);
        }

        #endregion

        #region LNURL Encoding Tests

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

        #endregion

        #region BoltCard Tests

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
                catch (Exception)
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

            var res = BoltCardHelper.ExtractUidAndCounterFromP(pValue, aesDecryptKey, out _);

            Assert.Equal(expectedUidHex, res.Value.uid);
            Assert.Equal(expectedCtr, res.Value.counter);

            var cmacIsValid = BoltCardHelper.CheckCmac(res.Value.rawUid, res.Value.rawCtr, aesCmacKey, cValue, out _);
            Assert.True(cmacIsValid, "CMAC validation failed");
        }

        #endregion

        #region LNURLJsonOptions Tests

        [Fact]
        public void LNURLJsonOptionsDefaultIsSingleton()
        {
            var options1 = LNURLJsonOptions.Default;
            var options2 = LNURLJsonOptions.Default;
            Assert.Same(options1, options2);
        }

        [Fact]
        public void CreateOptionsReturnsNewInstance()
        {
            var options1 = LNURLJsonOptions.CreateOptions();
            var options2 = LNURLJsonOptions.CreateOptions();
            Assert.NotSame(options1, options2);
        }

        #endregion
    }
}
