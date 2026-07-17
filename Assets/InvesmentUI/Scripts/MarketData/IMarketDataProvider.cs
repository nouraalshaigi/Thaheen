using System;
using System.Collections;

namespace InvestmentTowerUI.MarketData
{
    // Isolated market-data layer (Assets/InvesmentUI/Scripts/MarketData). Nothing outside this
    // folder issues a web request directly - CompanyCardView/other UI views, InvestmentTowerUIController
    // and the CompanyListPanel/CompanyDetailsPanel builders only ever read InvestmentCompany.price/
    // dailyChangePercent (updated in place by SaudiMarketService) or subscribe to
    // SaudiMarketService.OnQuotesUpdated. See YahooFinanceMarketProvider for the one concrete
    // implementation used in the game.
    public interface IMarketDataProvider
    {
        // One reusable request method for any symbol - callers must not hardcode a separate
        // implementation per symbol. Must be started as a Unity coroutine (e.g. via
        // MonoBehaviour.StartCoroutine or by yielding it from another coroutine).
        IEnumerator FetchQuote(string symbol, float timeoutSeconds, Action<SaudiStockQuote> onSuccess, Action<string> onFailure);
    }
}
