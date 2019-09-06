

namespace PennApps19.AzureFunction
{
    public interface ICommandFlagResponse : ICommandResponse
    {
        bool Success { get; set; }
    }
}
