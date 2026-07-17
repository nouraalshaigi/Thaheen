using System.Collections.Generic;
using CityHud;
using UnityEditor;
using UnityEngine;

namespace Dhaheen.Editor
{
    // DEVELOPMENT ONLY. Sends the documented hard-coded test session (see the Dhaheen Unity
    // Integration Guide's "First Integration Test") and logs the parsed Arabic report to the
    // Console, also pushing it into the live AI Report panel if the game is running so the full
    // round-trip can be seen, not just logged. This is intentionally an Editor menu item rather
    // than a runtime button - there is no test button anywhere in the shipped game.
    //
    // Only usable in Play Mode: DhaheenApiClient's request runs as a coroutine, which needs
    // Unity's runtime update loop.
    internal static class DhaheenDevTools
    {
        [MenuItem("Tools/Dhaheen/Send Test Session (Dev Only)", true)]
        private static bool ValidateIsPlaying() => EditorApplication.isPlaying;

        [MenuItem("Tools/Dhaheen/Send Test Session (Dev Only)")]
        private static void SendTestSession()
        {
            DhaheenGameSession testSession = new DhaheenGameSession
            {
                player_id = "unity_test_player",
                age = 12,
                language = "ar",
                goal = new DhaheenGoal { type = "saving", target_amount = 1000 },
                starting_balance = 1000,
                ending_balance = 1170,
                decisions = new List<DhaheenDecision>
                {
                    new DhaheenDecision { scenario_id = "S01", scenario_type = "income", choice = "save", amount = 300, response_time_seconds = 4 },
                    new DhaheenDecision { scenario_id = "S02", scenario_type = "shopping", choice = "spend", amount = 200, response_time_seconds = 2, item_type = "want" },
                    new DhaheenDecision { scenario_id = "S03", scenario_type = "investment", choice = "invest", amount = 150, response_time_seconds = 5, risk_level = "medium" },
                    new DhaheenDecision { scenario_id = "S04", scenario_type = "emergency", choice = "use_emergency_fund", amount = 180, response_time_seconds = 3, emergency_success = true },
                }
            };

            Debug.Log("DhaheenDevTools: sending hard-coded test session to " + "https://dhaheen-ai-analyzer.onrender.com/analyze ...");

            DhaheenApiClient.GetOrCreate().AnalyzeGameSession(
                testSession,
                "Development Test Session",
                result =>
                {
                    Debug.Log($"DhaheenDevTools: success. personality={result.personality}, report_source={result.report_source}");
                    Debug.Log($"DhaheenDevTools: report title={result.report?.title}");
                    Debug.Log($"DhaheenDevTools: report summary={result.report?.summary}");
                    Debug.Log($"DhaheenDevTools: report tip={result.report?.tip}");

                    if (CityHudController.Instance != null)
                    {
                        CityHudController.Instance.OpenAiReportPanel();
                        DhaheenResultsUI resultsUI = CityHudController.Instance.GetComponentInChildren<DhaheenResultsUI>(true);
                        if (resultsUI != null) resultsUI.DisplayResult(result);
                    }
                },
                (kind, error) => Debug.LogError($"DhaheenDevTools: test session failed ({kind}).\n{error}"));
        }
    }
}
