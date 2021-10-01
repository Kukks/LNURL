using System;
using Xunit;

namespace LNURL.Tests
{
    public class UnitTest1
    {
        [Theory]
        [InlineData("kukks@btcpay.kukks.org", "https://btcpay.kukks.org/.wellknown/lnurlp/kukks")]
        [InlineData("kukks@tor.onion","http://tor.onion/.wellknown/lnurlp/kukks")]
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
    }
}