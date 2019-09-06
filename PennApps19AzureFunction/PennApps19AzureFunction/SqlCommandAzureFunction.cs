using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Pluralize.NET.Core;

namespace PennApps19.AzureFunction
{
    public static class SqlCommandAzureFunction
    {
        public static Pluralizer p = new Pluralizer();

        [FunctionName("SqlCommandAzureFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log,
            ExecutionContext context)
        {
            log.LogInformation("PennApps19");

            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            string sqldb_connection = config.GetConnectionString("sqldb_connection");

            log.LogInformation(sqldb_connection);

            ICommandResponse response;

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogInformation(requestBody);

            using (SqlConnection connection = new SqlConnection(sqldb_connection))
            {
                connection.Open();

                LogHttpRequestBody(connection, requestBody);

                dynamic eventData = JsonConvert.DeserializeObject(requestBody);

                if (eventData == null)
                {
                    return new BadRequestObjectResult(new UnknownCommandResponse("eventData").ToJsonString());
                }

                string unescapedJson = ((string)eventData.data).Replace(@"\", "");
                dynamic jsonRequest = JsonConvert.DeserializeObject(unescapedJson);

                if (jsonRequest == null || jsonRequest.command == null || jsonRequest.data == null)
                {
                    return new BadRequestObjectResult(new UnknownCommandResponse("data tag"));
                }

                string command = jsonRequest.command;
                dynamic data = jsonRequest.data;

                switch (command)
                {
                    case Commands.FindItem:
                        response = FindItem(data, connection, log);
                        break;

                    case Commands.FindTags:
                        response = FindTags(data, connection, log);
                        break;

                    case Commands.InsertItem:
                        response = InsertItem(data, connection, log);
                        break;

                    case Commands.RemoveItem:
                        response = RemoveItem(data, connection, log);
                        break;

                    case Commands.AddTags:
                        response = AddTags(data, connection, log);
                        break;

                    case Commands.UpdateQuantity:
                        response = UpdateQuantity(data, connection, log);
                        break;

                    case Commands.SetQuantity:
                        response = SetQuantity(data, connection, log);
                        break;

                    case Commands.ShowAllBoxes:
                        response = ShowAllBoxes(connection, log);
                        break;

                    case Commands.BundleWith:
                        response = BundleWith(data, connection, log);
                        break;

                    case Commands.HowMany:
                        response = HowMany(data, connection, log);
                        break;

                    default:
                        response = new UnknownCommandResponse(command);
                        break;
                }

                try
                {
                    var requestResponseLogString = "INSERT INTO dbo.Commands ([DateCreated], [Command], [DataIn], [DataOut]) VALUES (@param1, @param2, @param3, @param4)";
                    using (SqlCommand sqlCommand = new SqlCommand())
                    {
                        sqlCommand.Connection = connection;
                        sqlCommand.CommandText = requestResponseLogString;
                        sqlCommand.Parameters.AddWithValue("@param1", DateTime.Now);
                        sqlCommand.Parameters.AddWithValue("@param2", command);
                        sqlCommand.Parameters.AddWithValue("@param3", Convert.ToString(data));
                        sqlCommand.Parameters.AddWithValue("@param4", response);
                        sqlCommand.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    log.LogInformation(ex.Message);
                }
            }

            return new OkObjectResult(response.ToJsonString());
        }

        public static FindItemResponse FindItem(dynamic jsonRequestData, SqlConnection connection, ILogger log)
        {
            string item = jsonRequestData;

            string nameKey = QueryHelper.Instance.SingularizeAndLower(item);

            List<Item> items = new List<Item>();

            var queryString = $"SELECT Name,Quantity,Row,Col FROM dbo.Items WHERE Items.NameKey LIKE @param1";

            using (SqlCommand command = new SqlCommand())
            {
                command.Connection = connection;
                command.CommandText = queryString;
                command.Parameters.AddWithValue("@param1", nameKey);

                SqlDataReader reader = command.ExecuteReader();
                try
                {
                    while (reader.Read())
                    {
                        items.Add(
                            new Item
                            {
                                Name = (string)reader[Dbo.Items.Name],
                                Quantity = (int)reader[Dbo.Items.Quantity],
                                Row = (int)reader[Dbo.Items.Row],
                                Col = (int)reader[Dbo.Items.Col]
                            });
                    }

                    if (items.Count > 0)
                    {
                        return new FindItemResponse(items);
                    }

                    // Todo: match tags extracted from input word and return top 3
                }
                catch (Exception ex)
                {
                    log.LogInformation(ex.Message);
                }
                finally
                {
                    reader.Close();
                }
            }

            return new FindItemResponse(null);
        }

        // Done. May be removed.
        public static FindItemResponse TryFindItem(string item, SqlConnection conn, ILogger log)
        {
            string singularizedItem = QueryHelper.Instance.SingularizeAndLower(item);

            List<Item> items = new List<Item>();

            var queryString = $"SELECT * FROM dbo.Items WHERE Items.NameKey LIKE @param1";

            using (SqlCommand command = new SqlCommand())
            {
                command.Connection = conn;
                command.CommandText = queryString;
                command.Parameters.AddWithValue("@param1", singularizedItem);

                SqlDataReader reader = command.ExecuteReader();
                try
                {
                    while (reader.Read())
                    {
                        items.Add(
                            new Item
                            {
                                Name = (string)reader[Dbo.Items.Name],
                                Quantity = (int)reader[Dbo.Items.Quantity],
                                Row = (int)reader[Dbo.Items.Row],
                                Col = (int)reader[Dbo.Items.Col],
                                IsSmallBox = (bool)reader[Dbo.Items.IsSmallBox],
                                DateCreated = (DateTime)reader[Dbo.Items.DateCreated],
                                LastUpdated = (DateTime)reader[Dbo.Items.LastUpdated]
                            });
                    }
                    return new FindItemResponse(items);
                }
                catch (Exception ex)
                {
                    log.LogInformation(ex.Message);

                    return new FindItemResponse(null);
                }
                finally
                {
                    reader.Close();
                }
            }
        }

        public static FindTagsResponse FindTags(dynamic jsonRequestData, SqlConnection connection, ILogger log, int maxResults = 10)
        {
            string words = jsonRequestData;

            return FindTags(new TagSet(words), connection, log, maxResults);
        }

        public static FindTagsResponse FindTags(TagSet tagSet, SqlConnection connection, ILogger log, int maxResults = 10)
        {
            if (tagSet.Count == 0) return new FindTagsResponse(-1, null);

            // Take a string of words: "Green motor driver"
            // Extract a HashSet of tags: HashSet<string> = { "Green", "motor", "driver" }
            // Format as params for SQL query, to defend against SQL injection attacks:
            //     "@param2,@param3,@param4"
            string paramList = string.Join(",", tagSet.Select((_, index) => $"@param{index + 2}"));
            log.LogInformation(paramList);

            var queryString = $@"
SELECT i.NameKey, i.Name, i.Quantity, i.Row, i.Col, t.TagsMatched
FROM dbo.Items i JOIN
(
    SELECT NameKey, COUNT(NameKey) TagsMatched
    FROM dbo.Tags
    WHERE Tag IN({paramList})
    GROUP BY NameKey
) t ON i.NameKey = t.NameKey
ORDER BY t.TagsMatched DESC";

            using (SqlCommand command = new SqlCommand())
            {
                command.Connection = connection;
                command.CommandText = queryString;
                command.Parameters.AddWithValue("@param1", maxResults);

                int index = 2;
                foreach (string tag in tagSet)
                {
                    command.Parameters.AddWithValue($"@param{index++}", tag);
                }

                SqlDataReader reader = command.ExecuteReader();
                try
                {
                    int i = 0;

                    List<int[]> coordsAndMatches = new List<int[]>();
                    while (reader.Read() && i++ < maxResults)
                    {
                        coordsAndMatches.Add(new int[] { (int)reader["Row"], (int)reader["Col"], (int)reader["TagsMatched"] });
                    }

                    return new FindTagsResponse(tagSet.Count, coordsAndMatches);
                }
                catch (Exception ex)
                {
                    log.LogInformation(ex.Message);
                    return new FindTagsResponse(tagSet.Count, null);
                }
                finally
                {
                    reader.Close();
                }
            }
        }

        // 1. Check if item exists, if yes, return the box it's in.
        // 2. Query for currently used boxes, find an empty box if one exists, and return it's row/column location
        // 3. Insert an entry into the Items table with the row/column info
        // 4. If successful, insert entries into the Tags table with the words from the item name as tags
        // 4. Return a response with data indicating the insert was successful, and the row/column info
        //
        // Format of info can be:
        // a. "<item name>"
        // b. "<item name> into a <small box|big box> with tags <tag0 tag1 tag2 ...>"
        // c. "<item name> with tags <tag0 tag1 tag2 ...> into a <small box|big box>"
        //
        // Todo: Singularize item
        public static ICommandResponse InsertItem(dynamic jsonRequestData, SqlConnection connection, ILogger log)
        {
            string info = jsonRequestData["Info"];
            int quantity = jsonRequestData["Quantity"];

            string infoLower = info.ToLowerInvariant();

            bool hasBox = TryGetBoxInfo(infoLower, out int boxIndex, out string boxSearch, out bool useSmallBox);
            bool hasTags = TryGetTagsInfo(infoLower, boxIndex, out int tagsIndex, out TagSet tagSet);
            string itemName = GetItemInfo(info, hasBox, hasTags, boxIndex, tagsIndex);

            tagSet.ParseAndUnionWith(itemName);

            FindItemResponse findItemResponse = FindItem(itemName, connection, log);

            if (findItemResponse.Count > 0)
            {
                return findItemResponse;
            }

            // Item doesn't exist; insert.
            // Find existing boxes
            var sqlAllConsumedBoxes = string.Format("SELECT DISTINCT ROW,COL FROM dbo.Items");
            MatrixModel matrix = new MatrixModel();

            using (SqlCommand command = new SqlCommand(sqlAllConsumedBoxes, connection))
            {
                SqlDataReader reader = command.ExecuteReader();

                try
                {
                    while (reader.Read())
                    {
                        matrix.AddItem((int)reader[Dbo.Items.Row], (int)reader[Dbo.Items.Col]);
                    }
                }
                finally
                {
                    reader.Close();
                }
            }

            var (row, col) = matrix.GetNextAvailableBox(useSmallBox);

            if (row == -1 && col == -1)
            {
                return new InsertItemResponse(false);
            }

            Item item = new Item(itemName, quantity, row, col, useSmallBox);

            return InsertItemWithTags(item, tagSet, connection, log);
        }

        public static InsertItemResponse InsertItemWithTags(Item item, TagSet tags, SqlConnection connection, ILogger log)
        {
            bool insertSucceeded = TryInsertItem(item, connection, log);

            if (!insertSucceeded)
            {
                return new InsertItemResponse(false);
            }

            // Todo: Revert adding to dbo.Items if inserting to dbo.Items fails
            int tagsAdded = InsertTags(connection, item.Name, tags);

            return new InsertItemResponse(insertSucceeded && tagsAdded > 0, item.Row.Value, item.Col.Value, item.Name);
        }
        
        public static RemoveItemResponse RemoveItem(dynamic jsonRequestData, SqlConnection connection, ILogger log)
        {
            string item = (string)jsonRequestData;

            FindItemResponse resp = TryFindItem(item, connection, log);

            if (resp.Count == 0)
            {
                return new RemoveItemResponse(false, item);
            }

            var queryString = $@"
DELETE FROM dbo.Tags  WHERE Tags.NameKey  LIKE @param1;
DELETE FROM dbo.Items WHERE Items.NameKey LIKE @param1;";

            using (SqlCommand command = new SqlCommand())
            {
                command.Connection = connection;
                command.CommandText = queryString;
                command.Parameters.AddWithValue("@param1", QueryHelper.Instance.SingularizeAndLower(item));

                int itemsRemoved = command.ExecuteNonQuery();
                
                if (itemsRemoved > 0)
                {
                    Item i = resp.Result.First();
                    return new RemoveItemResponse(itemsRemoved > 0, item, i.Row, i.Col);
                }
                else
                {
                    return new RemoveItemResponse(false, item);
                }
            }
        }

        // 1. Verify item exists
        // 2. Add tags
        public static AddTagsResponse AddTags(dynamic jsonRequestData, SqlConnection connection, ILogger log)
        {
            string data = jsonRequestData;
            string item = string.Empty;
            TagSet tagSet;

            if (data.Contains(" to "))
            {
                string[] tagsAndItem = data.Split(" to ", StringSplitOptions.RemoveEmptyEntries);
                item = tagsAndItem[1];
                tagSet = new TagSet(tagsAndItem[0]);
            }
            else if (data.Contains(" add tags "))
            {
                string[] itemAndTags = data.Split(" add tags ", StringSplitOptions.RemoveEmptyEntries);
                item = itemAndTags[0];
                tagSet = new TagSet(itemAndTags[1]);
            }
            else
            {
                return new AddTagsResponse(false, -1);
            }
            
            string itemLower = item.ToLowerInvariant();

            if (!ItemExists(item, connection, log))
            {
                return new AddTagsResponse(false, -1);
            }
            
            int tagsAdded = InsertTags(connection, item, tagSet);

            return new AddTagsResponse(true, tagsAdded);
        }

        // Todo: Return Command.SetQuantity
        public static FindItemResponse SetQuantity(dynamic jsonRequestData, SqlConnection connection, ILogger log)
        {
            string item = jsonRequestData["Item"];
            int quantity = jsonRequestData["Quantity"];

            var setQuantityQuery = $@"
UPDATE dbo.Items
SET Items.Quantity = @param1
WHERE Items.NameKey LIKE @param2";

            using (SqlCommand command = new SqlCommand())
            {
                command.Connection = connection;
                command.CommandText = setQuantityQuery;
                command.Parameters.AddWithValue("@param1", quantity);
                command.Parameters.AddWithValue("@param2", QueryHelper.Instance.SingularizeAndLower(item));

                if (command.ExecuteNonQuery() == 0)
                {
                    return new FindItemResponse(null);
                }
            }

            // Return the item, so the display lights the box
            return FindItem(item, connection, log);
        }

        // Todo: Return Command.UpdateQuantity
        public static FindItemResponse UpdateQuantity(dynamic jsonRequestData, SqlConnection connection, ILogger log)
        {
            string item = jsonRequestData["Item"];
            int quantity = jsonRequestData["Quantity"];
            bool adding = jsonRequestData["Add"];

            if (!adding)
            {
                quantity *= -1;
            }

            var updateQuantityQuery = $@"
UPDATE dbo.Items
SET Items.Quantity = Items.Quantity + @param1
WHERE Items.NameKey LIKE @param2";

            using (SqlCommand command = new SqlCommand())
            {
                command.Connection = connection;
                command.CommandText = updateQuantityQuery;
                command.Parameters.AddWithValue("@param1", quantity);
                command.Parameters.AddWithValue("@param2", QueryHelper.Instance.SingularizeAndLower(item));

                if (command.ExecuteNonQuery() == 0)
                {
                    return new FindItemResponse(null);
                }
            }

            // Return the item, so the display lights the box
            return FindItem(item, connection, log);
        }

        public static ShowAllBoxesResponse ShowAllBoxes(SqlConnection connection, ILogger log)
        {
            string allBoxesQuery = $@"SELECT DISTINCT Row,Col FROM dbo.Items";

            using (SqlCommand command = new SqlCommand(allBoxesQuery, connection))
            {
                SqlDataReader reader = command.ExecuteReader();

                try
                {
                    StringBuilder sb = new StringBuilder();

                    while(reader.Read())
                    {
                        sb.Append((char)((int)reader[Dbo.Items.Row] + 'a'));
                        sb.Append((char)((int)reader[Dbo.Items.Col] + 'a'));
                    }

                    string coords = sb.ToString();
                    
                    return new ShowAllBoxesResponse(coords.Length / 2, coords);
                }
                catch (Exception ex)
                {
                    log.LogInformation(ex.Message);

                    return new ShowAllBoxesResponse();
                }
                finally
                {
                    reader.Close();
                }
            }
        }

        // Formats:
        // "<new item> with <old item> add tags <tag0 tag1 tag2 ...>
        // "<new item> with tags <tag0 tag1 tag2 ...>
        public static BundleWithResponse BundleWith(dynamic jsonRequestData, SqlConnection connection, ILogger log)
        {
            string info = jsonRequestData["Info"];
            int quantity = jsonRequestData["Quantity"];

            if (info.Contains(" with tags "))
            {
                return BundleWithTags(info, quantity, connection, log);
            }
            else if (info.Contains(" with "))
            {
                return BundleWithItem(info, quantity, connection, log);
            }
            else
            {
                return new BundleWithResponse();
            }
        }

        public static BundleWithResponse BundleWithItem(string text, int quantity, SqlConnection connection, ILogger log)
        {
            string newItem = string.Empty;
            string existingItem = string.Empty;
            TagSet tagSet;

            if (text.Contains(" add tags "))
            {
                string[] itemsAndTags = text.Split(" add tags ", StringSplitOptions.RemoveEmptyEntries);
                string[] newAndExistingItem = itemsAndTags[0].Split(" with ", StringSplitOptions.RemoveEmptyEntries);

                newItem = newAndExistingItem[0].Trim();
                existingItem = newAndExistingItem[1].Trim();

                tagSet = new TagSet(itemsAndTags[1]);
            }
            else
            {
                string[] newAndExistingItem = text.Split(" with ", StringSplitOptions.RemoveEmptyEntries);

                newItem = newAndExistingItem[0].Trim();
                existingItem = newAndExistingItem[1].Trim();
                tagSet = new TagSet(newItem);
            }

            FindItemResponse foundItems = TryFindItem(existingItem, connection, log);
            
            if (foundItems.Count == 1)
            {
                Item foundItem = foundItems.Result[0];

                Item item = new Item
                {
                    Name = newItem,
                    Quantity = quantity,
                    Row = foundItem.Row.Value,
                    Col = foundItem.Col.Value,
                    IsSmallBox = foundItem.IsSmallBox.Value
                };

                if (TryInsertItem(item, connection, log))
                {
                    InsertTags(connection, newItem, tagSet);

                    FindItemResponse resp = FindItem(newItem, connection, log);

                    if (resp.Count > 0)
                    {
                        return new BundleWithResponse(true, newItem, quantity, existingItem, item.Row, item.Col);
                    }
                    else
                    {
                        return new BundleWithResponse(false, newItem, quantity, existingItem);
                    }
                }
            }

            return new BundleWithResponse();
        }

        public static BundleWithResponse BundleWithTags(string text, int quantity, SqlConnection connection, ILogger log)
        {
            string[] itemAndTags = text.Split(" with tags ", StringSplitOptions.RemoveEmptyEntries);

            if (itemAndTags.Length != 2)
            {
                return new BundleWithResponse();
            }

            string newItem = itemAndTags[0].Trim();
            TagSet existingItemTags = new TagSet(itemAndTags[1]);

            // Take a string of words: "Green motor driver"
            // Extract a HashSet of tags: HashSet<string> = { "Green", "motor", "driver" }
            // Format as params for SQL query, to defend against SQL injection attacks:
            //     "@param2,@param3,@param4"
            string paramList = string.Join(",", existingItemTags.Select((tag, index) => $"@param{index+2}"));
            log.LogInformation(paramList);

            int maxResults = 3;

            var queryString = $@"
SELECT TOP (@param1) i.Name, i.Quantity, i.Row, i.Col, i.IsSmallBox, t.TagsMatched
FROM dbo.Items i JOIN
(
    SELECT NameKey, COUNT(NameKey) TagsMatched
    FROM dbo.Tags
    WHERE Tag IN({paramList})
    GROUP BY NameKey
) t ON i.NameKey = t.NameKey
ORDER BY t.TagsMatched DESC";

            List<TaggedItem> items = new List<TaggedItem>();

            using (SqlCommand command = new SqlCommand())
            {
                command.Connection = connection;
                command.CommandText = queryString;
                command.Parameters.AddWithValue("@param1", maxResults);

                int index = 2;
                foreach (string tag in existingItemTags)
                {
                    command.Parameters.AddWithValue($"@param{index++}", tag);
                }
                
                SqlDataReader reader = command.ExecuteReader();
                try
                {
                    while (reader.Read())
                    {
                        items.Add(new TaggedItem
                        {
                            Name = (string)reader["Name"],
                            Row = (int)reader["Row"],
                            Col = (int)reader["Col"],
                            TagsMatched = (int)reader["TagsMatched"],
                            IsSmallBox = (bool)reader["IsSmallBox"]
                        });
                    }
                }
                catch (Exception ex)
                {
                    log.LogInformation(ex.Message);
                    return new BundleWithResponse();
                }
                finally
                {
                    reader.Close();
                }
            }

            IEnumerable<TaggedItem> fullyMatchedItems = items.Where(a => a.TagsMatched.Value == existingItemTags.Count);

            if (fullyMatchedItems.Count() == 1)
            {
                TaggedItem existingItem = fullyMatchedItems.First();
                Item insertItem = new Item(
                    newItem, 
                    quantity, 
                    existingItem.Row.Value, 
                    existingItem.Col.Value, 
                    existingItem.IsSmallBox.Value);

                TagSet newItemTags = new TagSet(newItem);

                InsertItemResponse resp = InsertItemWithTags(insertItem, newItemTags, connection, log);

                if (resp.Success)
                {
                    return new BundleWithResponse(true, newItem, quantity, existingItem.Name, insertItem.Row, insertItem.Col);
                }
                else
                {
                    return new BundleWithResponse(false, newItem, quantity, existingItem.Name);
                }
            }

            return new BundleWithResponse();
        }

        public static HowManyResponse HowMany(dynamic jsonRequestData, SqlConnection connection, ILogger log)
        {
            string itemName = jsonRequestData;

            FindItemResponse itemResponse = TryFindItem(itemName, connection, log);
            if (itemResponse.Count == 1)
            {
                Item i = itemResponse.Result.First();
                return new HowManyResponse(true, i.Name, i.Quantity, i.Row, i.Col);
            }
            else
            {
                return new HowManyResponse(false, itemName);
            }
        }


        /* Build a SQL insert statement supporting multiple insert values, without duplicating any entries:
             MERGE INTO dbo.Tags AS Target
             USING(VALUES (@param1, @param2),(@param1, @param3)) AS Source (Name, Tag)
             ON Target.Name = Source.Name AND Target.Tag = Source.Tag
             WHEN NOT MATCHED BY Target THEN
             INSERT(Name, Tag) VALUES(Source.Name, Source.Tag);

            After substitution:
            USING(VALUES ('AA Battery', 'aa'),('AA Battery', 'battery')) AS Source (Name, Tag)
        */
        private static int InsertTags(SqlConnection connection, string item, TagSet tagSet)
        {
            string insertTagsCommand = $@"
MERGE INTO dbo.Tags AS Target
USING(VALUES {string.Join(",", tagSet.Select((_, index) => $"(@param1, @param{index + 2})"))}) AS Source (NameKey, Tag)
ON Target.NameKey = Source.NameKey AND Target.Tag = Source.Tag
WHEN NOT MATCHED BY Target THEN
INSERT(NameKey, Tag) VALUES(Source.NameKey, Source.Tag);";

            int tagsAdded = 0;
            using (SqlCommand sqlCommand = new SqlCommand())
            {
                sqlCommand.Connection = connection;
                sqlCommand.CommandText = insertTagsCommand;
                sqlCommand.Parameters.AddWithValue("@param1", QueryHelper.Instance.SingularizeAndLower(item));
                int i = 2;
                foreach (string tag in tagSet)
                {
                    sqlCommand.Parameters.AddWithValue($"@param{i++}", tag);
                }
                tagsAdded = sqlCommand.ExecuteNonQuery();
            }

            return tagsAdded;
        }
        
        // Done.
        public static bool ItemExists(string itemName, SqlConnection connection, ILogger log)
        {
            string nameKey = QueryHelper.Instance.SingularizeAndLower(itemName);

            string itemExistsQuery = $@"
SELECT CASE WHEN EXISTS (
    SELECT *
    FROM dbo.Items
    WHERE Items.NameKey LIKE @param1
)
THEN CAST(1 AS BIT)
ELSE CAST(0 AS BIT) END";

            using (SqlCommand command = new SqlCommand())
            {
                command.Connection = connection;
                command.CommandText = itemExistsQuery;
                command.Parameters.AddWithValue("@param1", nameKey);

                SqlDataReader reader = command.ExecuteReader();
                try
                {
                    return reader.HasRows;
                }
                catch (Exception)
                {
                    return false;
                }
                finally
                {
                    reader.Close();
                }
            }
        }

        // Done.
        public static bool TryInsertItem(Item item, SqlConnection conn, ILogger log)
        {
            var sqlInsertString = @"
INSERT INTO dbo.Items([NameKey], [Name], [Quantity], [Row], [Col], [IsSmallBox], [DateCreated], [LastUpdated])
VALUES (@param1, @param2, @param3, @param4, @param5, @param6, @param7, @param8)";

            try
            {
                bool insertSucceeded = false;
                using (SqlCommand sqlCommand = new SqlCommand())
                {
                    sqlCommand.Connection = conn;
                    sqlCommand.CommandText = sqlInsertString;
                    sqlCommand.Parameters.AddWithValue("@param1", item.NameKey);
                    sqlCommand.Parameters.AddWithValue("@param2", item.Name);
                    sqlCommand.Parameters.AddWithValue("@param3", item.Quantity.Value);
                    sqlCommand.Parameters.AddWithValue("@param4", item.Row.Value);
                    sqlCommand.Parameters.AddWithValue("@param5", item.Col.Value);
                    sqlCommand.Parameters.AddWithValue("@param6", item.IsSmallBox.Value);
                    sqlCommand.Parameters.AddWithValue("@param7", DateTime.UtcNow);
                    sqlCommand.Parameters.AddWithValue("@param8", DateTime.UtcNow);
                    insertSucceeded = sqlCommand.ExecuteNonQuery() > 0;
                }

                return insertSucceeded;
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.Message);
                return false;
            }
        }

        // InsertItem helper methods
        public static bool TryGetBoxInfo(string info, out int startIndex, out string searchTerm, out bool useSmallBox)
        {
            string[] boxPrefix = { "into a", "in a" };
            BoxType[] boxTypes =
            {
                new BoxType("big", false), new BoxType("large", false),
                new BoxType("small", true), new BoxType("little", true)
            };
            string[] boxNames = { "box", "container" };
            foreach (string prefix in boxPrefix)
            {
                foreach (BoxType boxType in boxTypes)
                {
                    foreach (string name in boxNames)
                    {
                        string searchString = $" {prefix} {boxType.Type} {name}";
                        int index = info.IndexOf(searchString);
                        if (index != -1)
                        {
                            useSmallBox = boxType.IsSmallBox;
                            searchTerm = searchString;
                            startIndex = index;
                            return true;
                        }
                    }
                }
            }

            searchTerm = string.Empty;
            useSmallBox = true;
            startIndex = -1;
            return false;
        }

        public static bool TryGetTagsInfo(string info, int boxIndex, out int startIndex, out TagSet tags)
        {
            string tagsTag = " with tags ";
            startIndex = info.IndexOf(tagsTag);

            if (startIndex == -1)
            {
                tags = new TagSet();
                return false;
            }

            string tagsString;
            int tagsStartIndex = startIndex + tagsTag.Length;

            if (startIndex < boxIndex)
            {
                tagsString = info.Substring(tagsStartIndex, boxIndex - tagsStartIndex);
            }
            else
            {
                tagsString = info.Substring(tagsStartIndex);
            }

            if (!string.IsNullOrEmpty(tagsString))
            {
                tags = new TagSet(tagsString);
                return true;
            }
            else
            {
                tags = new TagSet();
                return false;
            }
        }

        public static string GetItemInfo(string info, bool hasBox, bool hasTags, int boxIndex, int tagsIndex)
        {
            string item = string.Empty;
            if (hasBox && hasTags)
            {
                item = info.Substring(0, tagsIndex < boxIndex ? tagsIndex : boxIndex);
                return item;
            }
            else if (hasBox)
            {
                item = info.Substring(0, boxIndex);
                return item;
            }
            else if (hasTags)
            {
                item = info.Substring(0, tagsIndex);
                return item;
            }
            return info;
        }

        public static void LogHttpRequestBody(SqlConnection connection, string requestBody)
        {
            var httpRequestString = $"INSERT INTO dbo.HttpRequests ([HttpRequestBody], [DateCreated]) VALUES (@param1, @param2)";
            using (SqlCommand sqlCommand = new SqlCommand())
            {
                sqlCommand.Connection = connection;
                sqlCommand.CommandText = httpRequestString;
                sqlCommand.Parameters.AddWithValue("@param1", requestBody);
                sqlCommand.Parameters.AddWithValue("@param2", DateTime.Now);
                sqlCommand.ExecuteNonQuery();
            }
        }
    }
}
