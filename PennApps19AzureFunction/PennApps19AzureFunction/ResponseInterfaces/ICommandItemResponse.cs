

namespace PennApps19.AzureFunction
{
    using System.Collections.Generic;

    public interface ICommandItemResponse
    {
        int Count { get; }

        List<Item> Result { get; set; }
    }
}
