using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace InvestmentTowerUI.MarketData
{
    // Talks to Yahoo Finance's public chart endpoint - no API key required, so none is exposed
    // anywhere in this project (see SaudiMarketService's Inspector fields). One reusable request
    // method (FetchQuote) is used for every symbol; nothing here hardcodes a per-symbol
    // implementation. Yahoo is a prototype data source only - callers (SaudiMarketService) are
    // responsible for falling back to cached/sample data on failure; this class only ever reports
    // success or failure, it never throws out of FetchQuote.
    public class YahooFinanceMarketProvider : IMarketDataProvider
    {
        private const string ChartEndpoint = "https://query1.finance.yahoo.com/v8/finance/chart/";

        public IEnumerator FetchQuote(string symbol, float timeoutSeconds, Action<SaudiStockQuote> onSuccess, Action<string> onFailure)
        {
            string encodedSymbol = UnityWebRequest.EscapeURL(symbol);
            string url = $"{ChartEndpoint}{encodedSymbol}?interval=1d&range=5d";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onFailure?.Invoke($"{symbol}: {request.error}");
                    yield break;
                }

                SaudiStockQuote quote = TryParseQuote(symbol, request.downloadHandler.text);
                if (quote == null)
                {
                    onFailure?.Invoke($"{symbol}: could not parse Yahoo response");
                    yield break;
                }

                onSuccess?.Invoke(quote);
            }
        }

        private static SaudiStockQuote TryParseQuote(string symbol, string json)
        {
            try
            {
                YahooChartResponse response = JsonUtility.FromJson<YahooChartResponse>(json);
                YahooChartMeta meta = response?.chart?.result != null && response.chart.result.Length > 0
                    ? response.chart.result[0].meta
                    : null;
                if (meta == null) return null;

                double previousClose = meta.chartPreviousClose != 0d ? meta.chartPreviousClose : meta.previousClose;
                double current = meta.regularMarketPrice;
                double change = current - previousClose;
                double changePercent = previousClose != 0d ? (change / previousClose) * 100d : 0d;

                return new SaudiStockQuote
                {
                    symbol = symbol,
                    currentPrice = (float)current,
                    previousClose = (float)previousClose,
                    dailyChange = (float)change,
                    dailyChangePercent = (float)changePercent,
                    marketTimestampUnix = meta.regularMarketTime,
                    localFetchTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"YahooFinanceMarketProvider: failed to parse quote for '{symbol}' - {ex.Message}");
                return null;
            }
        }
    }
}
