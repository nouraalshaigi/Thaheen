using System;

namespace InvestmentTowerUI.MarketData
{
    // Minimal DTO shape for Yahoo's chart endpoint response - only the fields SaudiStockQuote
    // actually needs (see YahooFinanceMarketProvider). Parsed with UnityEngine.JsonUtility, which
    // is already built into Unity (com.unity.modules.jsonserialize) and is fully sufficient for
    // this fixed, well-known response shape, so no extra JSON package (Newtonsoft or otherwise)
    // was added to the project - see IMarketDataProvider's header comment.
    //
    // Example (trimmed): { "chart": { "result": [ { "meta": {
    //   "regularMarketPrice": 28.4, "chartPreviousClose": 28.06, "regularMarketTime": 1699999999
    // } } ], "error": null } }
    [Serializable]
    public class YahooChartResponse
    {
        public YahooChart chart;
    }

    [Serializable]
    public class YahooChart
    {
        public YahooChartResult[] result;
        public YahooChartError error;
    }

    [Serializable]
    public class YahooChartError
    {
        public string code;
        public string description;
    }

    [Serializable]
    public class YahooChartResult
    {
        public YahooChartMeta meta;
    }

    [Serializable]
    public class YahooChartMeta
    {
        public string currency;
        public string symbol;
        public double regularMarketPrice;
        public double chartPreviousClose;
        public double previousClose;
        public long regularMarketTime;
    }
}
