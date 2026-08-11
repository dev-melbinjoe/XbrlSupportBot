using System.Threading.Tasks;

namespace XbrlSupportBot.AIServices
{
    public interface ILocalLlmSynthesisService
    {
        //Task EnsureModelLoadedAsync();
        Task<string> GenerateStepByStepGuideAsync(string description, string rca);

        Task<string> GenerateGeneralResponseAsync(string query);
    }
}
