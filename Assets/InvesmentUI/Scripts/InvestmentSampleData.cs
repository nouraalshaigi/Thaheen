using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InvestmentTowerUI
{
    // Temporary, isolated sample/session data for the Investment Tower UI. Companies and
    // holdings/transactions here are plain static state (not a ScriptableObject asset, not
    // written to disk), so they reset automatically to their initial values on every Unity
    // domain reload - including every time Play Mode stops - with no extra reset code required.
    // AvailableBalance is the one exception: it proxies to the player's real wallet (see
    // InvestmentWallet) rather than being sample data itself.
    //
    // This is deliberately the ONLY place that knows about sample prices and in-memory
    // holdings/transactions. When real market data and real buy/sell persistence are implemented
    // later, only this file (and the handful of call sites in InvestmentTowerUIController that
    // call its methods) need to change - no UI script or prefab needs to be touched.
    //
    // Per the explicit portfolio-availability rule: Holdings/Transactions start EMPTY. A holding
    // is only ever created by RecordPurchase, which only ever runs when the player presses the
    // (temporary) purchase-confirmation button - never pre-filled, never sample data.
    public static class InvestmentSampleData
    {
        public const string RiskLow = "منخفضة";
        public const string RiskMedium = "متوسطة";
        public const string RiskHigh = "عالية";

        public static readonly List<InvestmentCompany> Companies = new List<InvestmentCompany>
        {
            new InvestmentCompany
            {
                id = "alinma",
                nameArabic = "الإنماء",
                sectorArabic = "الخدمات المالية",
                logoLetters = "ال",
                logoColor = new Color32(0x2E, 0x7D, 0x5B, 0xFF),
                price = 28.40f,
                dailyChangePercent = 1.2f,
                riskLevelArabic = RiskMedium,
                descriptionArabic = "بنك الإنماء يقدم خدمات مصرفية إسلامية متكاملة للأفراد والشركات في المملكة العربية السعودية.",
                yahooSymbol = "1150.SR",
            },
            new InvestmentCompany
            {
                id = "aramco",
                nameArabic = "أرامكو السعودية",
                sectorArabic = "الطاقة",
                logoLetters = "أر",
                logoColor = new Color32(0x8B, 0x2E, 0x2E, 0xFF),
                price = 25.80f,
                dailyChangePercent = -0.4f,
                riskLevelArabic = RiskLow,
                descriptionArabic = "أكبر شركة نفط وغاز في العالم، تنتج وتصدر النفط الخام والمنتجات البترولية عالمياً.",
                yahooSymbol = "2222.SR",
            },
            new InvestmentCompany
            {
                id = "stc",
                nameArabic = "STC",
                sectorArabic = "الاتصالات",
                logoLetters = "STC",
                logoColor = new Color32(0x2C, 0x6E, 0x9E, 0xFF),
                price = 42.50f,
                dailyChangePercent = 0.8f,
                riskLevelArabic = RiskLow,
                descriptionArabic = "شركة الاتصالات السعودية، أكبر مزود لخدمات الاتصالات والإنترنت في المملكة العربية السعودية.",
                yahooSymbol = "7010.SR",
            },
        };

        // Real wallet balance - see InvestmentWallet for where this actually comes from
        // (StartFlow.PlayerDataManager.Data.currentAvailableMoney, with a temporary
        // Inspector-serialized fallback only until onboarding is connected).
        public static float AvailableBalance
        {
            get => InvestmentWallet.CurrentBalance;
            private set => InvestmentWallet.CurrentBalance = value;
        }

        public static readonly List<InvestmentHolding> Holdings = new List<InvestmentHolding>();
        public static readonly List<InvestmentTransaction> Transactions = new List<InvestmentTransaction>();

        public static bool HasAnyHoldings => Holdings.Count > 0;

        public static InvestmentHolding FindHolding(InvestmentCompany company) =>
            Holdings.FirstOrDefault(h => h.company == company);

        // Creates the temporary in-memory holding used to test the portfolio flow, and deducts
        // the cost from the player's real wallet balance via PlayerDataManager.TrySpendMoney.
        // Holding/transaction persistence beyond the current session is still out of scope.
        public static InvestmentHolding RecordPurchase(InvestmentCompany company, int shares)
        {
            if (company == null || shares <= 0) return null;

            float cost = company.price * shares;
            // Centralized wallet check (StartFlow.PlayerDataManager) - never spends twice, never
            // goes negative, and refreshes the City HUD itself. Returns null (no holding, no
            // transaction) if the player can't actually afford it.
            if (!StartFlow.PlayerDataManager.GetOrCreate().TrySpendMoney(cost)) return null;

            InvestmentHolding holding = FindHolding(company);
            if (holding == null)
            {
                holding = new InvestmentHolding { company = company, shares = shares, averagePrice = company.price };
                Holdings.Add(holding);
            }
            else
            {
                float previousTotal = holding.averagePrice * holding.shares;
                holding.shares += shares;
                holding.averagePrice = (previousTotal + cost) / holding.shares;
            }

            Transactions.Insert(0, new InvestmentTransaction
            {
                company = company,
                type = InvestmentTransactionType.Buy,
                shares = shares,
                pricePerShare = company.price,
                timestamp = DateTime.Now,
            });

            return holding;
        }

        public static void RecordSale(InvestmentHolding holding, int shares)
        {
            if (holding == null || shares <= 0 || shares > holding.shares) return;

            StartFlow.PlayerDataManager.GetOrCreate().AddMoney(holding.company.price * shares);
            holding.shares -= shares;

            Transactions.Insert(0, new InvestmentTransaction
            {
                company = holding.company,
                type = InvestmentTransactionType.Sell,
                shares = shares,
                pricePerShare = holding.company.price,
                timestamp = DateTime.Now,
            });

            if (holding.shares <= 0)
                Holdings.Remove(holding);
        }
    }
}
