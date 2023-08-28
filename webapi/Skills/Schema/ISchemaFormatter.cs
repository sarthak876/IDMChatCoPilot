// Copyright (c) Microsoft. All rights reserved.

namespace CopilotChat.WebApi.Skills.Schema;

using System.IO;
using System.Threading.Tasks;

internal interface ISchemaFormatter
{
    Task WriteAsync(TextWriter writer, SchemaDefinition schema);
}
