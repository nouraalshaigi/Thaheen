using System;
using System.Collections.Generic;

namespace Dhaheen
{
    // Serializable request/response models for the deployed Dhaheen AI Financial Behavior
    // Analyzer (POST https://dhaheen-ai-analyzer.onrender.com/analyze). Field names are exact
    // snake_case per the official Unity Integration Guide - the API is case-sensitive and
    // schema-strict, so nothing here may be renamed or restructured.
    //
    // Accepted values (do not invent alternatives):
    //   goal.type        : saving | investment | budgeting | emergency_fund
    //   scenario_type     : income | shopping | investment | emergency | budgeting
    //   choice            : save | spend | invest | wait | skip | split | borrow | use_emergency_fund
    //   risk_level        : low | medium | high
    //   item_type         : need | want | luxury

    [Serializable]
    public class DhaheenGoal
    {
        public string type;
        public float target_amount;
    }

    [Serializable]
    public class DhaheenDecision
    {
        public string scenario_id;
        public string scenario_type;
        public string choice;
        public float amount;
        public float response_time_seconds;

        // Optional fields - leave at their default (empty string / false) when not relevant to
        // this decision's scenario_type, exactly as the guide specifies.
        public string risk_level;
        public bool emergency_success;
        public string item_type;
    }

    [Serializable]
    public class DhaheenGameSession
    {
        public string player_id;
        public int age;
        public string language;
        public DhaheenGoal goal;
        public float starting_balance;
        public float ending_balance;
        public List<DhaheenDecision> decisions;
    }

    [Serializable]
    public class DhaheenScores
    {
        public float saving;
        public float spending;
        public float spending_control;
        public float investment;
        public float risk;
        public float impulse;
        public float emergency_readiness;
        public float goal_progress;
        public float overall;
    }

    [Serializable]
    public class DhaheenReport
    {
        public string title;
        public string summary;
        public string tip;
    }

    [Serializable]
    public class DhaheenAnalysisResponse
    {
        public bool success;
        public string personality;
        public DhaheenScores scores;
        public string[] recommendations;
        public string player_id;
        public DhaheenReport report;
        public string report_source;
    }
}
