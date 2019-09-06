
namespace PennApps19.AzureFunction
{
    public interface ICommandResponse
    {
        string Command { get; }

        string ToJsonString(bool indent = false);
    }
}
