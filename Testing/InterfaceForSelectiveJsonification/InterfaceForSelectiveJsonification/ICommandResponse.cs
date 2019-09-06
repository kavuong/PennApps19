using System;
using System.Collections.Generic;
using System.Text;

namespace PennApps19.AzureFunction
{
    public interface ICommandResponse
    {
        string Command { get; }

        bool Success { get; set; }

        string ToJsonString(bool indent = false);
    }
}
