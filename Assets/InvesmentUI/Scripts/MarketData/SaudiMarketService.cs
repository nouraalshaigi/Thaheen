using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InvestmentTowerUI.MarketData
{
    // Orchestrates the isolated market-data flow for exactly the three tracked Saudi symbols:
    // YahooFinanceMarketProvider -> SaudiMarketService (this class) -> InvestmentSampleData.Companies
    // (matched by InvestmentCompany.yahooSymbol) -> OnQuotesUpdated -> CompanyListPanel /
    // CompanyDetailsPanel / portfolio calculations, all of which simply re-read
    // InvestmentCompany.price/dailyChangePercent. No UI script issues a web request - only this
    // service and YahooFinanceMarketProvider do.
    //
    // Lives on the same GameObject as InvestmentTowerUIController (AI_Alinma_Investment_Tower_UI),
    // which looks it up via GetComponent and calls RequestOpenRefresh() from Open().
    public class SaudiMarketService : MonoBehaviour
    {
        // Yahoo Finance symbols for exactly the three companies this task is scoped to - never
        // add more here without also adding the company to InvestmentSampleData.Companies.
        private static readonly string[] TrackedSymbols = { "1150.SR", "2222.SR", "7010.SR" };

        [Header("Editor Settings")]
        [Tooltip("Automatically fetch fresh quotes every time the Investment Tower UI opens (subject to the cooldown below). Yahoo requires no API key for this endpoint, so none is exposed here.")]
        [SerializeField] private bool autoRefreshOnOpen = true;
        [Tooltip("Minimum seconds between the start of one refresh and the next - prevents duplicate/spammy requests, never fetches every frame.")]
        [SerializeField] private float refreshCooldownSeconds = 60f;
        [SerializeField] private float requestTimeoutSeconds = 10f;
        [Tooltip("If disabled, no network request is ever made - the game runs entirely on cached/sample prices.")]
        [SerializeField] private bool useYahooData = true;
        [Tooltip("Load the last cached quotes (Application.persistentDataPath) on startup, before any network request.")]
        [SerializeField] private bool useCachedData = true;
        [Tooltip("If a refresh fails and there is no cache either, keep InvestmentSampleData's built-in sample prices instead of showing nothing.")]
        [SerializeField] private bool useSampleDataOnFailure = true;
        [SerializeField] private bool verboseLogging = false;

        // Fired after every refresh attempt (success or failure) and once after applying cached
        // data on startup - CompanyListPanel/CompanyDetailsPanel refresh their already-existing
        // text in response; this event never carries UI logic itself.
        public event Action OnQuotesUpdated;

        // Arabic status line for the CompanyListPanel footer - see
        // InvestmentTowerUIController.HandleQuotesUpdated.
        public string StatusText { get; private set; } = string.Empty;

        private IMarketDataProvider provider;
        private readonly Dictionary<string, SaudiStockQuote> lastGoodQuotes = new Dictionary<string, SaudiStockQuote>();
        private float lastFetchRealtime = -999f;
        private bool fetchInProgress;

        private void Awake()
        {
            provider = new YahooFinanceMarketProvider();

            if (useCachedData)
            {
                Dictionary<string, SaudiStockQuote> cached = MarketQuoteCache.Load();
                if (cached.Count > 0)
                {
                    foreach (KeyValuePair<string, SaudiStockQuote> kvp in cached)
                        lastGoodQuotes[kvp.Key] = kvp.Value;
                    ApplyQuotesToCompanies(lastGoodQuotes.Values, "cache");
                    OnQuotesUpdated?.Invoke();
                }
            }
        }

        // Called by InvestmentTowerUIController.Open() - fetches only if AutoRefreshOnOpen is on
        // and the cooldown has elapsed; safe to call every time the UI opens.
        public void RequestOpenRefresh()
        {
            if (!autoRefreshOnOpen) return;
            TryRefreshAll();
        }

        public void TryRefreshAll()
        {
            if (!useYahooData)
            {
                Log("UseYahooData is disabled - staying on cached/sample prices.");
                return;
            }
            if (fetchInProgress)
            {
                Log("a refresh is already in progress - ignoring duplicate request.");
                return;
            }
            if (Time.realtimeSinceStartup - lastFetchRealtime < refreshCooldownSeconds)
            {
                Log($"refresh cooldown ({refreshCooldownSeconds}s) still active - skipping.");
                return;
            }

            StartCoroutine(RefreshAllCoroutine());
        }

        private IEnumerator RefreshAllCoroutine()
        {
            fetchInProgress = true;
            lastFetchRealtime = Time.realtimeSinceStartup;

            StatusText = ArabicTextUtility.Format("جاري تحديث الأسعار...");
            OnQuotesUpdated?.Invoke();

            int successCount = 0;
            foreach (string symbol in TrackedSymbols)
            {
                yield return provider.FetchQuote(symbol, requestTimeoutSeconds,
                    quote =>
                    {
                        lastGoodQuotes[symbol] = quote;
                        successCount++;
                    },
                    error => Debug.LogWarning($"SaudiMarketService: {error}"));
            }

            if (successCount > 0)
            {
                MarketQuoteCache.Save(lastGoodQuotes.Values);
                ApplyQuotesToCompanies(lastGoodQuotes.Values, "yahoo");
                StatusText = ArabicTextUtility.Format("تم تحديث الأسعار");
                Debug.Log($"SaudiMarketService: refreshed {successCount}/{TrackedSymbols.Length} tracked symbols.");
            }
            else
            {
                // Deliberately does not touch InvestmentCompany.price/dailyChangePercent here -
                // whatever was already applied (cache, or InvestmentSampleData's built-in sample
                // values if useSampleDataOnFailure and nothing else ever loaded) stays as-is.
                StatusText = ArabicTextUtility.Format("تعذر تحديث الأسعار، تم عرض آخر بيانات متاحة");
                Debug.LogWarning(useSampleDataOnFailure
                    ? "SaudiMarketService: no symbols refreshed - keeping last valid/cached/sample prices."
                    : "SaudiMarketService: no symbols refreshed.");
            }

            fetchInProgress = false;
            OnQuotesUpdated?.Invoke();
        }

        private void ApplyQuotesToCompanies(IEnumerable<SaudiStockQuote> quotes, string source)
        {
            int applied = 0;
            foreach (SaudiStockQuote quote in quotes)
            {
                InvestmentCompany company = InvestmentSampleData.Companies.Find(c => c.yahooSymbol == quote.symbol);
                if (company == null) continue;

                company.price = quote.currentPrice;
                company.dailyChangePercent = quote.dailyChangePercent;
                applied++;
            }
            Log($"applied {applied} {source} quote(s) to tracked companies.");
        }

        private void Log(string message)
        {
            if (verboseLogging) Debug.Log($"SaudiMarketService: {message}");
        }
    }
}
