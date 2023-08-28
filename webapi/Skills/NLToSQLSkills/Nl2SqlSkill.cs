// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CopilotChat.WebApi.Skills.Schema;
using Microsoft.SemanticKernel;

namespace CopilotChat.WebApi.Skills.NLToSQLSkills;

public class Nl2SqlSkill
{
    private readonly IKernel _kernel;
    private readonly SqlQueryGenerator _queryGenerator;
    private readonly SqlSchemaProviderHarness _schemaProviderHarness;

    public Nl2SqlSkill(
        IKernel kernel,
        SqlSchemaProviderHarness schemaProviderHarness)
    {
        this._kernel = kernel;
        this._queryGenerator = new SqlQueryGenerator(this._kernel, Repo.RootConfigFolder);
        this._schemaProviderHarness = schemaProviderHarness;
    }

    public async Task<string> ExecuteAsync(string objective)
    {
        var schemaNames = new List<string>();
        schemaNames.Add("BomDemandSupply");
        //await this._schemaProviderHarness.CaptureSchemaAsync(
        //    "BomDemandSupply",
        //    "demand, supply, and component data for the suppliers.").ConfigureAwait(false);
        await SchemaProvider.InitializeAsync(
            this._kernel,
            schemaNames.Select(s => Path.Combine(Repo.RootConfigFolder, "schema", $"{s}.json"))).ConfigureAwait(false);

        var context = this._kernel.CreateNewContext();

        var query =
            await this._queryGenerator.SolveObjectiveAsync(
                 objective,
                    context).ConfigureAwait(false);

        context.Variables.TryGetValue(SqlQueryGenerator.ContextParamSchemaId, out var schemaId);

        return query ?? "NotFound";
    }
}
