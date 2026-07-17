using System;
using UnityEngine;

namespace InvestmentTowerUI
{
    // Plain data - not a ScriptableObject asset, so nothing here leaks state between Editor
    // Play sessions (same reasoning as StartFlow.PlayerSetupData).
    [Serializable]
    public class InvestmentCompany
    {
        public string id;
        public string nameArabic;
        public string sectorArabic;
        public string logoLetters;
        public Color logoColor;
        public float price;
        public float dailyChangePercent;
        public string riskLevelArabic;
        public string descriptionArabic;
        // Yahoo Finance chart symbol (e.g. "1150.SR") - see InvestmentTowerUI.MarketData.SaudiMarketService,
        // which matches quotes back to companies by this field and updates price/dailyChangePercent in place.
        public string yahooSymbol;

        public bool IsRiskLow => riskLevelArabic == InvestmentSampleData.RiskLow;
    }

    // One owned position in a single company. Average purchase price only changes when more
    // shares of the same company are bought; selling never changes it (standard average-cost
    // accounting), matching "متوسط التكلفة" in the Portfolio reference.
    public class InvestmentHolding
    {
        public InvestmentCompany company;
        public int shares;
        public float averagePrice;

        public float TotalInvested => shares * averagePrice;
        public float CurrentValue => shares * company.price;
        public float ProfitLoss => CurrentValue - TotalInvested;
        public float ProfitLossPercent => averagePrice > 0f ? (company.price - averagePrice) / averagePrice * 100f : 0f;
    }

    public enum InvestmentTransactionType { Buy, Sell }

    public class InvestmentTransaction
    {
        public InvestmentCompany company;
        public InvestmentTransactionType type;
        public int shares;
        public float pricePerShare;
        public DateTime timestamp;

        public float TotalValue => shares * pricePerShare;
    }
}
