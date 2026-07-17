using System;

namespace InvestmentTowerUI.MarketData
{
    // One symbol's latest known quote - produced by IMarketDataProvider, cached to disk by
    // MarketQuoteCache, applied onto the matching InvestmentCompany by SaudiMarketService.
    [Serializable]
    public class SaudiStockQuote
    {
        public string symbol;
        public float currentPrice;
        public float previousClose;
        public float dailyChange;
        public float dailyChangePercent;
        // Unix seconds (UTC) - Yahoo's own "last market timestamp" for this quote.
        public long marketTimestampUnix;
        // Unix seconds (UTC) - when this device fetched/cached the quote (not Yahoo's time).
        public long localFetchTimeUnix;
    }
}
