// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;

namespace CopilotChat.WebApi.Helpers;

public interface IOpenAIHelper
{
    Task<string> ExtractMeaningfulReplyFromQueryAndJsonResult(string query, string jsonString);
    Task<string> Search(string ques, string schema);
    Task<string> RewriteQuery(string query);
}
