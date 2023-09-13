// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using CopilotChat.WebApi.Extensions;
using CopilotChat.WebApi.Skills.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;

namespace CopilotChat.WebApi.Skills.NLToSQLSkills;

public class Nl2SqlSkill
{
    private readonly IKernel _kernel;
    private readonly SqlQueryGenerator _queryGenerator;
    private readonly SqlSchemaProviderHarness _schemaProviderHarness;
    private readonly IConfiguration _configuration;

    public Nl2SqlSkill(
        IKernel kernel,
        SqlSchemaProviderHarness schemaProviderHarness,
        IConfiguration configuration)
    {
        this._configuration = configuration;
        this._kernel = kernel;
        this._queryGenerator = new SqlQueryGenerator(this._kernel, Repo.RootConfigFolder);
        this._schemaProviderHarness = schemaProviderHarness;
    }

    public async Task<string> ExecuteAsync(string objective, string schema)
    {
        string? answer = string.Empty;
        var schemaNames = SchemaDefinitions.GetNames().ToArray();
        try
        {
            await SchemaProvider.InitializeAsync(
                this._kernel,
                schemaNames.Select(s => Path.Combine(Repo.RootConfigFolder, "schema", $"{s}.json"))).ConfigureAwait(false);

            var context = this._kernel.CreateNewContext();

            var query =
                await this._queryGenerator.SolveObjectiveAsync(
            objective,
            context).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(query))
            {
                using var dbConnection = new SqlConnection(this._configuration.GetConnectionString(schema));
                string clientIdKey = "AIService:" + schema + "ManagedIdentity";
                string managedIdentity = this._configuration.GetSection(clientIdKey).Get<string>();
                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = managedIdentity });
                var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://database.windows.net/" }));

                dbConnection.AccessToken = token.Token;
                using var dataSet = await dbConnection.QueryMultipleToDataSetAsync(query);
                var csvStringBuilder = new StringBuilder();

                foreach (DataTable dataTable in dataSet.Tables)
                {
                    var dataTableCsv = dataTable.GetCsv();

                    csvStringBuilder.AppendLine(dataTableCsv);
                    csvStringBuilder.AppendLine();
                    csvStringBuilder.AppendLine();
                }

                var dataSetCsv = csvStringBuilder.ToString().TrimEnd();

                answer = await this._queryGenerator.GetReplyForUserQueryAsync(query, dataSetCsv, objective, context).ConfigureAwait(false);
            }
            context.Variables.TryGetValue(SqlQueryGenerator.ContextParamSchemaId, out var schemaId);
        }
        catch (Exception ex)
        {
            return "Sorry!Unable to fetch any records";
        }
        return answer ?? "Sorry!Unable to fetch any records";
    }
}
