// Copyright (c) Microsoft. All rights reserved.

namespace CopilotChat.WebApi.Skills.NLToSQLSkills;

using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CopilotChat.WebApi.Skills.Schema;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

/// <summary>
/// Generate SQL query targeting Microsoft SQL Server.
/// </summary>
public sealed class SqlQueryGenerator
{
    public const string ContextParamObjective = "data_objective";
    public const string ContextParamSchema = "data_schema";
    public const string ContextParamSchemaId = "data_schema_id";
    public const string ContextParamQuery = "data_query";
    public const string ContextParamPlatform = "data_platform";
    public const string ContextParamError = "data_error";

    private const string ContentLabelQuery = "sql";
    private const string SkillName = "nl2sql";

    private readonly ISKFunction _promptGenerator;
    private readonly ISKFunction _formulateAnswer;
    private readonly ISemanticTextMemory _memory;

    public SqlQueryGenerator(IKernel kernel, string rootSkillFolder)
    {
        var functions = kernel.ImportSemanticSkillFromDirectory(rootSkillFolder, SkillName);
        this._promptGenerator = functions["generatequery"];
        this._formulateAnswer = functions["formulateanswer"];
        this._memory = kernel.Memory;

        kernel.ImportSkill(this, SkillName);
    }

    /// <summary>
    /// Attempt to produce a query for the given objective based on the registered schemas.
    /// </summary>
    /// <param name="objective">A natural language objective</param>
    /// <param name="context">A <see cref="SKContext"/> object</param>
    /// <returns>A SQL query (or null if not able)</returns>
    [SKFunction, Description("Generate a data query for a given objective and schema")]
    [SKName("GenerateQueryFromObjective")]
    public async Task<string?> SolveObjectiveAsync(string objective, SKContext context)
    {
        // Search for schema with best similarity match to the objective
        var recall =
            await this._memory.SearchAsync(
                SchemaProvider.MemoryCollectionName,
                objective,
                limit: 10,
                withEmbeddings: true).ToArrayAsync().ConfigureAwait(false);

        var best = recall.FirstOrDefault();
        if (best == null)
        {
            return null; // No schema / no query
        }

        var schemaName = best.Metadata.Id;
        var schemaText = best.Metadata.Text;
        var sqlPlatform = best.Metadata.AdditionalMetadata;

        context.Variables[ContextParamObjective] = objective;
        context.Variables[ContextParamSchema] = schemaText;
        context.Variables[ContextParamSchemaId] = schemaName;
        context.Variables[ContextParamPlatform] = sqlPlatform;

        // Generate query
        context = await this._promptGenerator.InvokeAsync(context).ConfigureAwait(false);

        // Parse result to handle 
        return context.GetResult(ContentLabelQuery);
    }

    [SKFunction, Description("Generate a user reply based on tabular data and sql query")]
    [SKName("GenerateReplyFromData")]
    public async Task<string?> GetReplyForUserQueryAsync(string sqlQuery, string data, string question, SKContext context)
    {
        context.Variables.Set("sql", sqlQuery);
        context.Variables.Set("data", data);
        context.Variables.Set("question", question);

        // Generate query
        context = await this._formulateAnswer.InvokeAsync(context).ConfigureAwait(false);

        // Parse result to handle 
        return context.Result;
    }
}
