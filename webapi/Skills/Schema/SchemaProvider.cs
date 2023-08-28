// Copyright (c) Microsoft. All rights reserved.

namespace CopilotChat.WebApi.Skills.Schema;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

/// <summary>
/// Responsible for loading the defined schemas into semantic memory.
/// </summary>
public static class SchemaProvider
{
    public const string MemoryCollectionName = "data-schemas";

    public static async Task InitializeAsync(IKernel kernel, IEnumerable<string> schemaPaths)
    {
        foreach (var schemaPath in schemaPaths)
        {
            var schema = await SchemaSerializer.ReadAsync(schemaPath).ConfigureAwait(false);

            var schemaText = await schema.FormatAsync(YamlSchemaFormatter.Instance).ConfigureAwait(false);

            await kernel.Memory.SaveInformationAsync(MemoryCollectionName, schemaText, schema.Name, additionalMetadata: schema.Platform).ConfigureAwait(false);
        }
    }

    public static async Task<List<string>> GetColumnNamesFromSchema(string schemaPath)
    {
        List<string> columnNames = new();
        var schema = await SchemaSerializer.ReadAsync(schemaPath).ConfigureAwait(false);
        foreach (var table in schema.Tables)
        {
            columnNames.Add(table.Name.Replace("dbo.", ""));
            foreach (var columnName in table.Columns)
            {
                columnNames.Add(columnName.Name);
            }
        }
        return columnNames;
    }

    public static async Task<List<string>> GetTableNamesFromSchema(string schemaPath)
    {
        List<string> tableNames = new();
        var schema = await SchemaSerializer.ReadAsync(schemaPath).ConfigureAwait(false);
        foreach (var table in schema.Tables)
        {
            tableNames.Add(table.Name.Replace("dbo.", ""));
        }
        return tableNames;
    }
}
