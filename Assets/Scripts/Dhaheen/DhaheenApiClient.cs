using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Dhaheen
{
    // Coarse classification of a failed request, so callers can show the right Arabic message
    // (see DhaheenGameTracker) without string-sniffing UnityWebRequest.error themselves.
    public enum DhaheenErrorKind { Network, Timeout, InvalidResponse, Http422, Other }

    // Sends the player's session to the deployed Dhaheen analyzer and parses the response.
    // Self-creating DontDestroyOnLoad singleton - same Awake-dedup/GetOrCreate pattern already
    // established by StartFlow.PlayerDataManager - so it survives scene changes (StartFlowScene
    // -> OGscene) without any manual scene setup and without ever creating a duplicate instance.
    // No API key lives here or anywhere in Unity; the OpenRouter key stays on the backend.
    public class DhaheenApiClient : MonoBehaviour
    {
        private const string AnalyzeUrl = "https://dhaheen-ai-analyzer.onrender.com/analyze";

        public static DhaheenApiClient Instance { get; private set; }

        public bool IsAnalyzing { get; private set; }

        public static DhaheenApiClient GetOrCreate()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("DhaheenApiClient");
            return go.AddComponent<DhaheenApiClient>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        public void AnalyzeGameSession(
            DhaheenGameSession session,
            string requestSource,
            Action<DhaheenAnalysisResponse> onSuccess,
            Action<DhaheenErrorKind, string> onError)
        {
            if (IsAnalyzing)
            {
                onError?.Invoke(DhaheenErrorKind.Other, "An analysis request is already running.");
                return;
            }

            StartCoroutine(SendAnalysisRequest(session, requestSource, onSuccess, onError));
        }

        private IEnumerator SendAnalysisRequest(
            DhaheenGameSession session,
            string requestSource,
            Action<DhaheenAnalysisResponse> onSuccess,
            Action<DhaheenErrorKind, string> onError)
        {
            IsAnalyzing = true;

            // Single, centralized cleanup gate for every outgoing session regardless of source
            // (real gameplay tracker or the dev-only test session) - see
            // DhaheenRequestSerializer's own header for why JsonUtility isn't used for requests.
            DhaheenRequestSerializer.Sanitize(session.decisions);

            if (!DhaheenRequestSerializer.Validate(session.decisions, out string validationError))
            {
                string abortMessage = $"Dhaheen request aborted before sending (source='{requestSource}'): {validationError}";
                Debug.LogError(abortMessage);
                IsAnalyzing = false;
                onError?.Invoke(DhaheenErrorKind.Other, abortMessage);
                yield break;
            }

            string requestJson = DhaheenRequestSerializer.ToJson(session);
            byte[] requestBody = Encoding.UTF8.GetBytes(requestJson);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"DhaheenApiClient: preparing request - source='{requestSource}', " +
                $"starting_balance={session.starting_balance:0.##}, ending_balance={session.ending_balance:0.##}");
            Debug.Log("DhaheenApiClient: sending to " + AnalyzeUrl);
            Debug.Log(requestJson);
#endif

            using UnityWebRequest request = new UnityWebRequest(AnalyzeUrl, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(requestBody);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 90;

            yield return request.SendWebRequest();

            IsAnalyzing = false;
            string responseText = request.downloadHandler != null ? request.downloadHandler.text : "";

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMessage =
                    $"Dhaheen API request failed.\n" +
                    $"Status: {request.responseCode}\n" +
                    $"Error: {request.error}\n" +
                    $"Response: {responseText}";
                Debug.LogError(errorMessage);

                DhaheenErrorKind kind;
                if (request.error != null && request.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                    kind = DhaheenErrorKind.Timeout;
                else if (request.responseCode == 422)
                    kind = DhaheenErrorKind.Http422;
                else if (request.result == UnityWebRequest.Result.ConnectionError)
                    kind = DhaheenErrorKind.Network;
                else
                    kind = DhaheenErrorKind.Other;

                if (kind == DhaheenErrorKind.Http422)
                {
                    // 422 means a field name or accepted value didn't match the schema - the
                    // exact outgoing JSON is required to diagnose it, per the integration guide.
                    Debug.LogError("DhaheenApiClient: 422 outgoing JSON was:\n" + requestJson);
                    Debug.LogError("DhaheenApiClient: 422 backend response was:\n" + responseText);
                }

                onError?.Invoke(kind, errorMessage);
                yield break;
            }

            try
            {
                DhaheenAnalysisResponse result = JsonUtility.FromJson<DhaheenAnalysisResponse>(responseText);
                if (result == null || !result.success)
                    throw new Exception("Invalid or unsuccessful API response.");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"DhaheenApiClient: HTTP {request.responseCode} success.");
                Debug.Log("DhaheenApiClient: response body:\n" + responseText);
#endif

                onSuccess?.Invoke(result);
            }
            catch (Exception exception)
            {
                string errorMessage =
                    $"Could not read Dhaheen response.\n" +
                    $"Error: {exception.Message}\n" +
                    $"Response: {responseText}";
                Debug.LogError(errorMessage);
                onError?.Invoke(DhaheenErrorKind.InvalidResponse, errorMessage);
            }
        }
    }
}
