using System.Collections.Generic;
using InvestmentTowerUI;
using TMPro;
using UnityEngine;

namespace Dhaheen
{
    // Fills the existing City_HUD AI Report card with a DhaheenAnalysisResponse, or a status
    // message while a request is loading/failed/waiting for decisions. Owns none of the panel's
    // open/close behavior (that stays on CityHud.CityHudController exactly as before) - this only
    // fills in content, using the project's existing Arabic shaping solution
    // (InvestmentTowerUI.ArabicTextUtility) for every dynamic string, exactly once per field.
    public class DhaheenResultsUI : MonoBehaviour
    {
        [Header("Status (loading / error / empty-decisions message)")]
        [SerializeField] private GameObject statusState;
        [SerializeField] private TMP_Text statusText;

        [Header("Result")]
        [SerializeField] private GameObject resultState;
        [SerializeField] private TMP_Text personalityText;
        [SerializeField] private TMP_Text reportTitleText;
        [SerializeField] private TMP_Text reportSummaryText;
        [SerializeField] private TMP_Text reportTipText;
        [SerializeField] private SummaryRowView overallScoreRow;
        [SerializeField] private SummaryRowView savingScoreRow;
        [SerializeField] private SummaryRowView spendingControlScoreRow;
        [SerializeField] private SummaryRowView investmentScoreRow;
        [SerializeField] private SummaryRowView emergencyReadinessScoreRow;

        [Header("Recommendations (dynamic, count is not known in advance)")]
        [SerializeField] private Transform recommendationsContent;
        [SerializeField] private TMP_Text recommendationTemplate;

        private readonly List<TMP_Text> recommendationRows = new List<TMP_Text>();

        private void Awake()
        {
            if (recommendationTemplate != null) recommendationTemplate.gameObject.SetActive(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogMissingReferences();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogMissingReferences()
        {
            if (statusState == null) Debug.LogWarning("DhaheenResultsUI: statusState is not wired.");
            if (statusText == null) Debug.LogWarning("DhaheenResultsUI: statusText is not wired.");
            if (resultState == null) Debug.LogWarning("DhaheenResultsUI: resultState is not wired.");
            if (personalityText == null) Debug.LogWarning("DhaheenResultsUI: personalityText is not wired.");
            if (reportTitleText == null) Debug.LogWarning("DhaheenResultsUI: reportTitleText is not wired.");
            if (reportSummaryText == null) Debug.LogWarning("DhaheenResultsUI: reportSummaryText is not wired.");
            if (reportTipText == null) Debug.LogWarning("DhaheenResultsUI: reportTipText is not wired.");
            if (overallScoreRow == null) Debug.LogWarning("DhaheenResultsUI: overallScoreRow is not wired.");
            if (savingScoreRow == null) Debug.LogWarning("DhaheenResultsUI: savingScoreRow is not wired.");
            if (spendingControlScoreRow == null) Debug.LogWarning("DhaheenResultsUI: spendingControlScoreRow is not wired.");
            if (investmentScoreRow == null) Debug.LogWarning("DhaheenResultsUI: investmentScoreRow is not wired.");
            if (emergencyReadinessScoreRow == null) Debug.LogWarning("DhaheenResultsUI: emergencyReadinessScoreRow is not wired.");
            if (recommendationsContent == null) Debug.LogWarning("DhaheenResultsUI: recommendationsContent is not wired.");
            if (recommendationTemplate == null) Debug.LogWarning("DhaheenResultsUI: recommendationTemplate is not wired.");
        }
#endif

        public void ShowLoading() => ShowStatus("جاري تحليل قراراتك المالية...");

        public void ShowMessage(string arabicMessage) => ShowStatus(arabicMessage);

        private void ShowStatus(string arabicMessage)
        {
            if (statusState != null) statusState.SetActive(true);
            if (resultState != null) resultState.SetActive(false);
            if (statusText != null) ArabicTextUtility.Apply(statusText, arabicMessage);
        }

        public void DisplayResult(DhaheenAnalysisResponse result)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("DhaheenResultsUI: DisplayResult reached.");
#endif
            if (statusState != null) statusState.SetActive(false);
            if (resultState != null) resultState.SetActive(true);

            if (personalityText != null)
                ArabicTextUtility.Apply(personalityText, TranslatePersonality(result.personality));

            DhaheenReport report = result.report ?? new DhaheenReport();
            if (reportTitleText != null) ArabicTextUtility.Apply(reportTitleText, report.title);
            if (reportSummaryText != null) ArabicTextUtility.Apply(reportSummaryText, report.summary);
            if (reportTipText != null) ArabicTextUtility.Apply(reportTipText, report.tip);

            DhaheenScores scores = result.scores ?? new DhaheenScores();
            SetScoreRow(overallScoreRow, "التقييم العام", scores.overall);
            SetScoreRow(savingScoreRow, "الادخار", scores.saving);
            SetScoreRow(spendingControlScoreRow, "التحكم بالإنفاق", scores.spending_control);
            SetScoreRow(investmentScoreRow, "الاستثمار", scores.investment);
            SetScoreRow(emergencyReadinessScoreRow, "الاستعداد للطوارئ", scores.emergency_readiness);

            SetRecommendations(result.recommendations);
        }

        // score is a plain 0-100 number from the backend - "82%" stays left-to-right (the number
        // leads), same safe pattern used everywhere else in this project.
        private static void SetScoreRow(SummaryRowView row, string labelArabic, float score)
        {
            if (row == null) return;
            row.Set(labelArabic, Mathf.RoundToInt(score) + "%");
        }

        private void SetRecommendations(string[] recommendations)
        {
            if (recommendationsContent == null || recommendationTemplate == null) return;

            int count = recommendations != null ? recommendations.Length : 0;
            while (recommendationRows.Count < count)
                recommendationRows.Add(Instantiate(recommendationTemplate, recommendationsContent));

            for (int i = 0; i < recommendationRows.Count; i++)
            {
                bool active = i < count;
                recommendationRows[i].gameObject.SetActive(active);
                if (active) ArabicTextUtility.Apply(recommendationRows[i], recommendations[i]);
            }
        }

        private static string TranslatePersonality(string personality)
        {
            switch (personality)
            {
                case "BALANCED_PLANNER": return "المخطط المتوازن";
                case "CAUTIOUS_SAVER": return "المدخر الحذر";
                case "RISK_TAKER_INVESTOR": return "المستثمر الجريء";
                case "IMPULSIVE_SPENDER": return "المنفق المتسرع";
                case "EMERGENCY_UNPREPARED": return "غير المستعد للطوارئ";
                default: return "المستكشف المالي";
            }
        }
    }
}
