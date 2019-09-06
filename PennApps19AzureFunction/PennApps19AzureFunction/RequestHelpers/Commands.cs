﻿using System;
using System.Collections.Generic;
using System.Text;

namespace PennApps19.AzureFunction
{
    public class Commands
    {
        public const string FindItem = "FindItem";
        public const string FindTags = "FindTags";
        public const string InsertItem = "InsertItem";
        public const string RemoveItem = "RemoveItem";
        public const string AddTags = "AddTags";
        public const string UpdateQuantity = "UpdateQuantity";
        public const string SetQuantity = "SetQuantity";
        public const string ShowAllBoxes = "ShowAllBoxes";
        public const string BundleWith = "BundleWith";
        public const string HowMany = "HowMany";
        public const string UnknownCommand = "UnknownCommand";
    }
}
