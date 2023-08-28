// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;

namespace CopilotChat.WebApi.Helpers;

public interface IDataHelper
{
    Task<string> SearchIdmTestDB(string query, string userInput, string dataSource);
    Task<string> FindDataSource(string ques);
    Task<string> QueryDataSource(string dataSource, string query, string userInput);
}
