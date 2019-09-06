﻿

namespace PennApps19.AzureFunction
{
    using Pluralize.NET.Core;
    using System;

    public class Item
    {
        public Item() { }

        public Item(string name, int quantity, int row, int col, bool isSmallBox)
        {
            this.Name = name;
            this.Quantity = quantity;
            this.Row = row;
            this.Col = col;
            this.IsSmallBox = isSmallBox;
            this.DateCreated = DateTime.Now;
            this.LastUpdated = DateTime.Now;
        }

        public Item(string name, int quantity, int row, int col, bool isSmallBox, DateTime dateCreated, DateTime lastUpdated)
        {
            this.Name = name;
            this.Quantity = quantity;
            this.Row = row;
            this.Col = col;
            this.IsSmallBox = isSmallBox;
            this.DateCreated = dateCreated;
            this.LastUpdated = lastUpdated;
        }

        public string Name { get; set; } = null;
        public int? Quantity { get; set; } = null;
        public int? Row { get; set; } = null;
        public int? Col { get; set; } = null;
        public bool? IsSmallBox { get; set; } = null;
        public DateTime? DateCreated { get; set; } = null;
        public DateTime? LastUpdated { get; set; } = null;

        // NameKey is used in SQL database as the primary key for dbo.Items
        // It is the lower-case, singularized version of the Name
        // Ex: Name = "Green LEDs", NameKey = "green led"
        public string NameKey
        {
            get
            {
                return string.IsNullOrEmpty(this.Name)
                    ? null
                    : QueryHelper.Instance.SingularizeAndLower(this.Name);
            }
        }

        /* 'ShouldSerialize' is used by Newtonsoft.Json to determine whether to serialize a property.
         * Using my nullables method, any combination of properties may be initialized when building the object for JSON serialization.
         * 
         *    Item item = new Item {
         *       Name = "foo",
         *       Quantity = 5
         *    };
         * 
         * A call to JsonConvert.SerializeObect(item) would produce:
         * 
         *    {"Name":"foo","Quantity":5}
         *    
         * The rest of the parameters would be ignored.
         * If Row, Col, etc. were assigned non-null values, they would be serialized as well.
         */

        public bool ShouldSerializeName()
        {
            return this.Name != null;
        }

        public bool ShouldSerializeQuantity()
        {
            return this.Quantity != null;
        }

        public bool ShouldSerializeRow()
        {
            return this.Row != null;
        }

        public bool ShouldSerializeCol()
        {
            return this.Col != null;
        }

        public bool ShouldSerializeIsSmallBox()
        {
            return this.IsSmallBox != null;
        }

        public bool ShouldSerializeDateCreated()
        {
            return this.DateCreated != null;
        }

        public bool ShouldSerializeLastUpdated()
        {
            return this.LastUpdated != null;
        }

        public bool ShouldSerializeSingularizedName()
        {
            return this.Name != null;
        }
    }
}
