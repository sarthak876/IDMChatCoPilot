// Copyright (c) Microsoft. All rights reserved.

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CopilotChat.WebApi.Helpers;

public class OpenAIHelper : IOpenAIHelper
{
    private readonly IConfiguration _config;

    public OpenAIHelper(IConfiguration config)
    {
        this._config = config;
    }

    public async Task<string> ExtractMeaningfulReplyFromQueryAndJsonResult(string query, string jsonString)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(jsonString))
            {
                var output = JsonSerializer.Deserialize<List<dynamic>>(jsonString);
                if (output?.Count > 50)
                {
                    jsonString = JsonSerializer.Serialize(output.FirstOrDefault(), new JsonSerializerOptions() { WriteIndented = true });
                }
            }
        }
        catch (Exception ex)
        {
            //swallow ex
            return "Sorry!Unable to fetch any records";
        }
        string openAiEndpoint = this._config.GetSection("AIService")["Endpoint"];
        string apiKey = this._config.GetSection("AIService")["Key"];
        string chatSchema = this._config.GetSection("AIService")["MeaningfulReply"];
        string prompts = string.Format(chatSchema, query, jsonString);
        Completions completions = null;
        try
        {
            OpenAIClient client = new OpenAIClient(
            new Uri(openAiEndpoint),
            new AzureKeyCredential(apiKey));

            // If streaming is not selected
            Response<Completions> completionsResponse = await client.GetCompletionsAsync(
                deploymentOrModelName: this._config.GetSection("AIService")["ChatBotDeploymentContainer"],
                new CompletionsOptions()
                {
                    Prompts = { prompts },
                    Temperature = (float)0.9,
                    MaxTokens = 256,
                    StopSequences = { "Human:", "AI:" },
                    NucleusSamplingFactor = 1,
                    FrequencyPenalty = 0,
                    PresencePenalty = 0,
                    GenerationSampleCount = 1,
                });
            completions = completionsResponse.Value;
        }
        catch (Exception ex)
        {
            //swallow ex
            return "Sorry!Unable to fetch any records";
        }
        string replyText = completions.Choices[0].Text;
        return replyText.Replace("AI Assistant:", "");
    }

    public async Task<string> Search(string ques, string schema)
    {
        string openAiEndpoint = this._config.GetSection("AIService")["Endpoint"];
        string apiKey = this._config.GetSection("AIService")["Key"];
        string prompts = string.Format(schema, ques);
        OpenAIClient client = new OpenAIClient(
        new Uri(openAiEndpoint),
        new AzureKeyCredential(apiKey));

        // If streaming is not selected
        Response<Completions> completionsResponse = await client.GetCompletionsAsync(
            deploymentOrModelName: this._config.GetSection("AIService")["DeploymentContainer"],
            new CompletionsOptions()
            {
                Prompts = { prompts },
                Temperature = 0,
                MaxTokens = 150,
                StopSequences = { "#", ";" },
                NucleusSamplingFactor = 1,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
                GenerationSampleCount = 1,
            });
        Completions completions = completionsResponse.Value;

        return "SELECT" + completions.Choices[0].Text;
    }

    public async Task<string> RewriteQuery(string query)
    {
        string openAiEndpoint = this._config.GetSection("AIService")["Endpoint"];
        string apiKey = this._config.GetSection("AIService")["Key"];
        string chatSchema = this._config.GetSection("AIService")["RewriteQuery"];
        string prompts = string.Format(chatSchema, query);
        OpenAIClient client = new OpenAIClient(
        new Uri(openAiEndpoint),
        new AzureKeyCredential(apiKey));

        // If streaming is not selected
        Response<Completions> completionsResponse = await client.GetCompletionsAsync(
            deploymentOrModelName: this._config.GetSection("AIService")["ChatBotDeploymentContainer"],
            new CompletionsOptions()
            {
                Prompts = { prompts },
                Temperature = (float)0.9,
                MaxTokens = 256,
                StopSequences = { "Human:", "AI:" },
                NucleusSamplingFactor = 1,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
                GenerationSampleCount = 1,
            });
        Completions completions = completionsResponse.Value;

        string replyText = completions.Choices[0].Text;
        replyText.Replace("AI Assistant:", "");
        return replyText.Substring(replyText.IndexOf("SELECT"));
    }
}
