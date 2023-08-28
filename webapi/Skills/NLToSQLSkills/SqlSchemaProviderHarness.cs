// Copyright (c) Microsoft. All rights reserved.

namespace CopilotChat.WebApi.Skills.NLToSQLSkills;

using System.IO;
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
    private const string BomDemandSupply = "BomDemandSupply";

    private IConfiguration _configuration;

    public SqlSchemaProviderHarness(IConfiguration configuration)
    {
        this._configuration = configuration;
    }

    public async Task CaptureSchemaAsync(string databaseKey, string? description, params string[] tableNames)
    {
        databaseKey = BomDemandSupply;
        var connectionString = this._configuration.GetConnectionString(databaseKey);
        using var connection = new SqlConnection(connectionString);
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = "18b131e3-ff7e-4225-9474-76af8f1e27cf" });
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
