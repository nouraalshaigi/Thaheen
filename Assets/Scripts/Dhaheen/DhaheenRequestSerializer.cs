using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Dhaheen
{
    // Builds the exact outgoing request JSON by hand instead of UnityEngine.JsonUtility.
    // JsonUtility has no concept of an optional field - it always emits every public field on
    // DhaheenDecision, so an unset item_type/risk_level serialized as "" instead of being
    // omitted, which the backend schema rejects with HTTP 422 for any non-shopping/non-
    // investment decision. This project deliberately avoids adding Newtonsoft.Json as a real
    // dependency (see Assets/InvesmentUI/Scripts/MarketData/YahooFinanceResponse.cs) - Newtonsoft
    // is only ever present transitively via com.unity.ai.assistant, not declared in
    // Packages/manifest.json, so this hand-rolled builder avoids relying on it.
    //
    // Sanitize()/Validate() are the single, centralized cleanup gate for EVERY outgoing session
    // regardless of where it came from (DhaheenGameTracker's real gameplay session or
    // DhaheenDevTools's hard-coded test session) - see DhaheenApiClient.SendAnalysisRequest,
    // the one place both callers funnel through.
    internal static class DhaheenRequestSerializer
    {
        // Forces item_type/risk_level/emergency_success to only ever hold a value that's
        // actually valid for that decision's scenario_type - clearing anything that doesn't
        // apply to null rather than leaving a stale/empty value behind.
        internal static void Sanitize(List<DhaheenDecision> decisions)
        {
            if (decisions == null) return;

            foreach (DhaheenDecision decision in decisions)
            {
                bool isShopping = decision.scenario_type == "shopping";
                bool isInvestment = decision.scenario_type == "investment";
                bool isEmergency = decision.scenario_type == "emergency";

                if (isShopping)
                {
                    bool validItemType = decision.item_type == "need" || decision.item_type == "want" || decision.item_type == "luxury";
                    if (!validItemType)
                    {
                        Debug.LogWarning($"DhaheenRequestSerializer: shopping decision '{decision.scenario_id}' had invalid item_type='{decision.item_type}' - forcing to 'want'.");
                        decision.item_type = "want";
                    }
                }
                else if (!string.IsNullOrEmpty(decision.item_type))
                {
                    Debug.LogWarning($"DhaheenRequestSerializer: decision '{decision.scenario_id}' (scenario_type={decision.scenario_type}) had an unexpected item_type='{decision.item_type}' - clearing it.");
                    decision.item_type = null;
                }

                if (!isInvestment && !string.IsNullOrEmpty(decision.risk_level))
                {
                    Debug.LogWarning($"DhaheenRequestSerializer: decision '{decision.scenario_id}' (scenario_type={decision.scenario_type}) had an unexpected risk_level='{decision.risk_level}' - clearing it.");
                    decision.risk_level = null;
                }
                // Investment's risk_level is intentionally left as-is here (no invented
                // fallback, unlike item_type's safe "want" default) - Validate() below is the
                // hard gate that catches a still-invalid value and aborts the send entirely.

                if (!isEmergency && decision.emergency_success)
                {
                    Debug.LogWarning($"DhaheenRequestSerializer: decision '{decision.scenario_id}' (scenario_type={decision.scenario_type}) had emergency_success=true - forcing to false.");
                    decision.emergency_success = false;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            for (int i = 0; i < decisions.Count; i++)
            {
                DhaheenDecision d = decisions[i];
                Debug.Log($"DhaheenRequestSerializer: cleaned decision[{i}] scenario_id={d.scenario_id}, scenario_type={d.scenario_type}, " +
                    $"choice={d.choice}, item_type='{d.item_type}', risk_level='{d.risk_level}', emergency_success={d.emergency_success}");
            }
#endif
        }

        // Hard-abort gate, run after Sanitize(). Should be unreachable in practice (Sanitize
        // already forces item_type to a safe default and MapRiskLevel in DhaheenGameTracker
        // always returns a valid risk_level) - this exists so a request is never sent with a
        // value that's still wrong, no matter how it got that way.
        internal static bool Validate(List<DhaheenDecision> decisions, out string error)
        {
            var problems = new List<string>();

            if (decisions != null)
            {
                foreach (DhaheenDecision decision in decisions)
                {
                    if (decision.scenario_type == "shopping")
                    {
                        bool validItemType = decision.item_type == "need" || decision.item_type == "want" || decision.item_type == "luxury";
                        if (!validItemType)
                            problems.Add($"shopping decision '{decision.scenario_id}' has invalid item_type='{decision.item_type}'");
                    }

                    if (decision.scenario_type == "investment")
                    {
                        bool validRisk = decision.risk_level == "low" || decision.risk_level == "medium" || decision.risk_level == "high";
                        if (!validRisk)
                            problems.Add($"investment decision '{decision.scenario_id}' has invalid risk_level='{decision.risk_level}'");
                    }
                }
            }

            error = problems.Count > 0 ? string.Join("; ", problems) : null;
            return problems.Count == 0;
        }

        // Hand-built JSON - never relies on Sanitize() alone to keep bad data out: item_type/
        // risk_level/emergency_success are gated directly by scenario_type here too, so even a
        // future bug in Sanitize() can't leak an empty optional field into the actual bytes sent.
        internal static string ToJson(DhaheenGameSession session)
        {
            var sb = new StringBuilder();
            sb.Append('{');

            AppendStringField(sb, "player_id", session.player_id);
            sb.Append(',');
            AppendNumberField(sb, "age", session.age);
            sb.Append(',');
            AppendStringField(sb, "language", session.language);
            sb.Append(',');

            sb.Append("\"goal\":{");
            AppendStringField(sb, "type", session.goal.type);
            sb.Append(',');
            AppendNumberField(sb, "target_amount", session.goal.target_amount);
            sb.Append("},");

            AppendNumberField(sb, "starting_balance", session.starting_balance);
            sb.Append(',');
            AppendNumberField(sb, "ending_balance", session.ending_balance);
            sb.Append(',');

            sb.Append("\"decisions\":[");
            List<DhaheenDecision> decisions = session.decisions;
            for (int i = 0; i < decisions.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendDecision(sb, decisions[i]);
            }
            sb.Append(']');

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendDecision(StringBuilder sb, DhaheenDecision d)
        {
            sb.Append('{');

            AppendStringField(sb, "scenario_id", d.scenario_id);
            sb.Append(',');
            AppendStringField(sb, "scenario_type", d.scenario_type);
            sb.Append(',');
            AppendStringField(sb, "choice", d.choice);
            sb.Append(',');
            AppendNumberField(sb, "amount", d.amount);
            sb.Append(',');
            AppendNumberField(sb, "response_time_seconds", d.response_time_seconds);

            if (d.scenario_type == "shopping" && !string.IsNullOrWhiteSpace(d.item_type))
            {
                sb.Append(',');
                AppendStringField(sb, "item_type", d.item_type);
            }

            if (d.scenario_type == "investment" && !string.IsNullOrWhiteSpace(d.risk_level))
            {
                sb.Append(',');
                AppendStringField(sb, "risk_level", d.risk_level);
            }

            if (d.scenario_type == "emergency")
            {
                sb.Append(',');
                AppendBoolField(sb, "emergency_success", d.emergency_success);
            }

            sb.Append('}');
        }

        private static void AppendStringField(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(key).Append("\":");
            if (value == null) sb.Append("null");
            else sb.Append('"').Append(Escape(value)).Append('"');
        }

        private static void AppendNumberField(StringBuilder sb, string key, float value)
        {
            sb.Append('"').Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendNumberField(StringBuilder sb, string key, int value)
        {
            sb.Append('"').Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendBoolField(StringBuilder sb, string key, bool value)
        {
            sb.Append('"').Append(key).Append("\":").Append(value ? "true" : "false");
        }

        private static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
