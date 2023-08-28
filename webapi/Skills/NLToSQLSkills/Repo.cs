﻿// Copyright (c) Microsoft. All rights reserved.

namespace CopilotChat.WebApi.Skills.NLToSQLSkills;

using System;
using System.IO;

/// <summary>
/// Utility class to assist in resolving file-system paths.
/// </summary>
internal static class Repo
{
    private static string RootFolder { get; } = GetRoot();

    public static string RootConfigFolder { get; } = $@"{Repo.RootFolder}\nl2sql.config";

    private static string GetRoot()
    {
        var current = Environment.CurrentDirectory;

        var folder = new DirectoryInfo(current);

        //while (!Directory.Exists(Path.Combine(folder.FullName, ".git")))
        //{
        //    folder =
        //        folder.Parent ??
        //        throw new DirectoryNotFoundException($"Unable to locate repo root folder: {current}");
        //}

        return folder.FullName;
    }
}
