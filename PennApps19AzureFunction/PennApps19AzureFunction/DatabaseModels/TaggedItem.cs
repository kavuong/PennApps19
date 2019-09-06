

namespace PennApps19.AzureFunction
{
    using System.Collections.Generic;

    public class TaggedItem : Item
    {
        public int? TagsMatched { get; set; }

        public HashSet<string> Tags { get; set; }
    }
}
