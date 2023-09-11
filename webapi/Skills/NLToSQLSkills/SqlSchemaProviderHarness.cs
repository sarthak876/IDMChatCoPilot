// Copyright (c) Microsoft. All rights reserved.

namespace CopilotChat.WebApi.Skills.NLToSQLSkills;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using CopilotChat.WebApi.Skills.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Harness for utilizing <see cref="SqlSchemaProvider"/> to capture live database schema
/// definitions: <see cref="SchemaDefinition"/>.
/// </summary>
public sealed class SqlSchemaProviderHarness
{
    //private const string BomDemandSupply = "BomDemandSupply";

    private IConfiguration _configuration;

    public SqlSchemaProviderHarness(IConfiguration configuration)
    {
        this._configuration = configuration;
    }

    public async Task CaptureSchemaAsync(string databaseKey, string? description, params string[] tableNames)
    {
        var connectionString = this._configuration.GetConnectionString(databaseKey);
        using var connection = new SqlConnection(connectionString);
        string clientIdKey = "AIService:" + databaseKey + "ManagedIdentity";
        string managedIdentity = this._configuration.GetSection(clientIdKey).Get<string>();
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = managedIdentity });
        var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://database.windows.net/" }));

        connection.AccessToken = token.Token;
        await connection.OpenAsync().ConfigureAwait(false);

        var provider = new SqlSchemaProvider(connection);

        var schema = await provider.GetSchemaAsync(description, tableNames).ConfigureAwait(false);

        await connection.CloseAsync().ConfigureAwait(false);

        // Capture YAML for inspection
        var yamlText = await schema.FormatAsync(YamlSchemaFormatter.Instance).ConfigureAwait(false);
        await this.SaveSchemaAsync("yaml", databaseKey, yamlText).ConfigureAwait(false);

        // Capture json for reserialization
        await this.SaveSchemaAsync("json", databaseKey, schema.ToJson()).ConfigureAwait(false);
    }

    public async Task CaptureDBSchemaAsync()
    {
        var schemaNames = SchemaDefinitions.GetNames().ToArray();
        foreach (var schema in schemaNames)
        {
            await this.CaptureSchemaAsync(
                schema,
                this._configuration.GetSection("AIService")[schema]).ConfigureAwait(false);
        }
    }

    private async Task SaveSchemaAsync(string extension, string databaseKey, string schemaText)
    {
        var fileName = Path.Combine(Repo.RootConfigFolder, "schema", $"{databaseKey}.{extension}");

        using var streamCompact =
            new StreamWriter(
                fileName,
                new FileStreamOptions
                {
                    Access = FileAccess.Write,
                    Mode = FileMode.Create,
                });

        await streamCompact.WriteAsync(schemaText).ConfigureAwait(false);
    }
}
