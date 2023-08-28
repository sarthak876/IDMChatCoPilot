// Copyright (c) Microsoft. All rights reserved.

using Azure.Core;
using Azure.Identity;
using CopilotChat.WebApi.Skills.NLToSQLSkills;
using CopilotChat.WebApi.Skills.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CopilotChat.WebApi.Helpers;

public class DataHelper : IDataHelper
{
    private readonly IConfiguration _config;
    private readonly IOpenAIHelper _openAIHelper;

    public DataHelper(IConfiguration config, IOpenAIHelper openAIHelper)
    {
        this._config = config;
        this._openAIHelper = openAIHelper;
    }

    public async Task<string> SearchIdmTestDB(string query, string userInput, string dataSource)
    {
        try
        {
            string originalQuery = query;
            //Call OpenAI again with instructions to generate query with union instead of join as join is resulting in duplicate records
            List<string> tableNames = await SchemaProvider.GetTableNamesFromSchema(
                         Path.Combine(Repo.RootConfigFolder, "schema", $"{dataSource}.json")).ConfigureAwait(false);
            //if (this.IsQueryContainsMultipleTables(query, tableNames))
            //{
            //    string schema = this._config.GetSection("AIService")["CrossTableQuery"];
            //    query = await this._openAIHelper.Search(query, schema);
            //}
            if (!string.IsNullOrWhiteSpace(query))
            {
                using (var conn = new SqlConnection(this._config.GetConnectionString("BomDemandSupply")))
                {
                    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = "18b131e3-ff7e-4225-9474-76af8f1e27cf" });
                    var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://database.windows.net/" }));

                    conn.AccessToken = token.Token;
                    SqlCommand cm = new(query, conn);
                    await conn.OpenAsync();

                    // Executing the SQL query  
                    using (var rdr = await cm.ExecuteReaderAsync())
                    {
                        string jsonString = await ToJSON(rdr);
                        return await this._openAIHelper.ExtractMeaningfulReplyFromQueryAndJsonResult(query, jsonString);
                    }
                };
            }
        }
        catch (Exception ex)
        {
            //if (ex?.Message?.ToLower()?.Contains("syntax") ?? false)
            //{
            //    string updatedQuery = await this._openAIHelper.RewriteQuery(query);
            //    return await this.SearchIdmTestDB(updatedQuery, userInput);
            //}
            //return ex.Message + ex.StackTrace;
            return "Sorry!Unable to fetch any records";
        }

        return "Sorry!Unable to fetch any records";
    }

    public async Task<string> FindDataSource(string ques)
    {
        if (!string.IsNullOrWhiteSpace(ques))
        {
            var dictionary = new Dictionary<string, List<string>>();
            var wordsExtractedFromInput = ExtractWordsFromUserInput(ques);
            var schemaNames = SchemaDefinitions.GetNames().ToArray();
            foreach (var schemaName in schemaNames)
            {
                var wordsExtractedFromIdmTestSchema = await this.ExtractWordsFromBomTableSchema(schemaName);
                dictionary.Add(schemaName, wordsExtractedFromIdmTestSchema);
            }

            foreach (var keyValuePair in dictionary)
            {
                foreach (var word in wordsExtractedFromInput)
                {
                    var columnNames = keyValuePair.Value;
                    if (columnNames?.Contains(word) ?? false)
                    {
                        return keyValuePair.Key;
                    }
                }
            }
        }
        return string.Empty;
    }

    public async Task<string> QueryDataSource(string dataSource, string query, string userInput)
    {
        return dataSource switch
        {
            "BomDemandSupply" => await this.SearchIdmTestDB(query, userInput, dataSource),
            _ => string.Empty,
        };
    }

    private async static Task<string> ToJSON(SqlDataReader reader)
    {
        var results = await GetSerialized(reader);
        return JsonSerializer.Serialize(results, new JsonSerializerOptions() { WriteIndented = true });
    }
    private async static Task<IEnumerable<Dictionary<string, object>>> GetSerialized(SqlDataReader reader)
    {
        var results = new List<Dictionary<string, object>>();
        var cols = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            cols.Add(reader.GetName(i));
        }

        while (await reader.ReadAsync())
        {
            results.Add(SerializeRow(cols, reader));
        }

        return results;
    }
    private static Dictionary<string, object> SerializeRow(IEnumerable<string> cols,
                                                    SqlDataReader reader)
    {
        var result = new Dictionary<string, object>();
        foreach (var col in cols)
        {
            result.Add(col, reader[col]);
        }

        return result;
    }

    private async Task<List<string>> ExtractWordsFromBomTableSchema(string schema)
    {
        List<string> allTableColumns = new();
        List<string>? finalList = null;
        List<string> columnNames = await SchemaProvider.GetColumnNamesFromSchema(
           Path.Combine(Repo.RootConfigFolder, "schema", $"{schema}.json")).ConfigureAwait(false);

        allTableColumns.AddRange(columnNames);

        foreach (string column in columnNames)
        {
            if (column.Contains("_"))
            {
                allTableColumns.Add(column.Replace("_", "").Trim().ToLower());
            }
            //Get list split by caps
            string[] words = ToNamingConvention(column.Trim());

            foreach (string word in words)
            {
                if (word == " ")
                {
                    continue;
                }
                // Convert the word to lowercase and remove any leading/trailing whitespace
                allTableColumns.Add(word.Trim().ToLower());
            }
        }

        //allTableColumns = allTableColumns.Select(t => Regex.Replace(t, @"\s+", "")).Select(t => t.ToLower()).Distinct().ToList();
        var engValues = this._config.GetSection("AIService:CommonEngWords").Get<List<string>>();

        //allTableColumns = allTableColumns.Except(engValues.Select(t => t.ToLower()).Distinct().ToList()).ToList();
        if (allTableColumns.Count > 0)
        {
            finalList = new List<string>();
            foreach (string temp in allTableColumns)
            {
                if (!engValues.Contains(temp.ToLower()))
                {
                    finalList.Add(temp);
                }

                string newtemp = temp + "s";
                finalList.Add(newtemp);
            }
        }
        return finalList.Distinct().ToList();
    }

    private static string[] ToNamingConvention(string s)
    {
        string[] words = s.Split(' ');
        if (!s.Any(char.IsDigit))
        {
            var r = new Regex(@"
            (?<=[A-Z])(?=[A-Z][a-z]) |
                (?<=[^A-Z])(?=[A-Z]) |
                (?<=[A-Za-z])(?=[^A-Za-z])", RegexOptions.IgnorePatternWhitespace);
            string converteds1 = r.Replace(s, "~");
            return converteds1.Split('~');
        }
        return words;
    }

    private static List<string> ExtractWordsFromUserInput(string ques)
    {
        string optimizedString = ques.Replace("_", "").Replace("?", "");
        optimizedString = string.Join(" ", optimizedString.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).ToList().Select(x => x.Trim().ToLower()));
        return optimizedString.Split(" ").ToList();
    }

    private bool IsQueryContainsMultipleTables(string query, List<string> tables)
    {
        int count = 0;
        foreach (var table in tables)
        {
            if (query.Contains(table))
            {
                count++;
            }
        }
        return count > 1;
    }
}
