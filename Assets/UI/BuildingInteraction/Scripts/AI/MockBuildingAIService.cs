using System.Threading.Tasks;

namespace BuildingInteractionSystem
{
    // Placeholder implementation used until a real, paid AI provider is wired up. Returns
    // simple, building-specific Arabic text so the UI (loading/response/error states) can be
    // built and tested end-to-end today.
    public class MockBuildingAIService : IBuildingAIService
    {
        private readonly int simulatedDelayMs;

        public MockBuildingAIService(int simulatedDelayMs = 700)
        {
            this.simulatedDelayMs = simulatedDelayMs;
        }

        public async Task<BuildingAIResponse> AskAsync(BuildingAIRequest request)
        {
            if (simulatedDelayMs > 0)
                await Task.Delay(simulatedDelayMs);

            if (string.IsNullOrWhiteSpace(request.playerQuestion))
                return BuildingAIResponse.Failure("الرجاء كتابة سؤال أولاً.");

            return new BuildingAIResponse
            {
                success = true,
                aiResponse = BuildResponse(request.buildingId),
                suggestedAction = BuildAction(request.buildingId),
                errorMessage = string.Empty
            };
        }

        private static string BuildResponse(BuildingId id)
        {
            switch (id)
            {
                case BuildingId.InvestmentTower:
                    return "هذا رد تجريبي: يمكن مقارنة الخيارات الاستثمارية من حيث المخاطر والعائد المتوقع قبل اتخاذ القرار.";
                case BuildingId.ShoppingMall:
                    return "هذا رد تجريبي: قبل الشراء، حاول التمييز بين الحاجة والرغبة، وتأكد من أن المبلغ يناسب الميزانية.";
                case BuildingId.CharityHouse:
                    return "هذا رد تجريبي: يمكن اختيار مبلغ للتبرع لا يؤثر على المصاريف الأساسية، مع ملاحظة أثره الإيجابي.";
                case BuildingId.NajdiHousePiggyBank:
                    return "هذا رد تجريبي: يمكن تحديد هدف ادخار بسيط وتقسيمه على عدة أشهر لتحقيقه تدريجيًا.";
                default:
                    return "هذا رد تجريبي من المساعد الذكي.";
            }
        }

        private static string BuildAction(BuildingId id)
        {
            switch (id)
            {
                case BuildingId.InvestmentTower:
                    return "اقتراح: قارن بين خيارين استثماريين قبل القرار.";
                case BuildingId.ShoppingMall:
                    return "اقتراح: أجّل الشراء يومًا واحدًا قبل التأكيد.";
                case BuildingId.CharityHouse:
                    return "اقتراح: خصص نسبة صغيرة وثابتة للتبرع كل شهر.";
                case BuildingId.NajdiHousePiggyBank:
                    return "اقتراح: ابدأ بادخار مبلغ صغير وثابت كل أسبوع.";
                default:
                    return string.Empty;
            }
        }
    }
}
