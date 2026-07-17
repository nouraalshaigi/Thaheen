using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace InvestmentTowerUI.MarketData
{
    // Persists the last successful quotes to Application.persistentDataPath as JSON (never
    // PlayerPrefs - see SaudiMarketService). Deliberately holds only the fields Yahoo is allowed
    // to provide (symbol/price/previousClose/change/changePercent/timestamps) - never Arabic
    // name, sector, description, risk level or logo, which stay local to InvestmentSampleData.
    public static class MarketQuoteCache
    {
        private const string FileName = "investment_market_cache.json";

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        [Serializable]
        private class CacheFile
        {
            public List<SaudiStockQuote> quotes = new List<SaudiStockQuote>();
        }

        public static void Save(IEnumerable<SaudiStockQuote> quotes)
        {
            try
            {
                var file = new CacheFile();
                foreach (SaudiStockQuote q in quotes)
                    if (q != null) file.quotes.Add(q);

                File.WriteAllText(FilePath, JsonUtility.ToJson(file));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"MarketQuoteCache: save failed - {ex.Message}");
            }
        }

        // Never throws and never returns null - callers can always foreach the result.
        public static Dictionary<string, SaudiStockQuote> Load()
        {
            var result = new Dictionary<string, SaudiStockQuote>();
            try
            {
                if (!File.Exists(FilePath)) return result;

                CacheFile file = JsonUtility.FromJson<CacheFile>(File.ReadAllText(FilePath));
                if (file?.quotes == null) return result;

                foreach (SaudiStockQuote q in file.quotes)
                    if (q != null && !string.IsNullOrEmpty(q.symbol))
                        result[q.symbol] = q;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"MarketQuoteCache: load failed - {ex.Message}");
            }
            return result;
        }
    }
}
