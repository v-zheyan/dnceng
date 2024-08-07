using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Azure.Data.Tables;
using Azure.Identity;

namespace RolloutScorer;

public class Utilities
{
    public const string KeyVaultUri = "https://engkeyvault.vault.azure.net";
    public const string GitHubPatSecretName = "BotAccount-dotnet-bot-repo-PAT";

    public static bool IssueContainsRelevantLabels(Issue issue, string issueLabel, string repoLabel, ILogger log = null, Microsoft.Extensions.Logging.LogLevel logLevel = Microsoft.Extensions.Logging.LogLevel.Information)
    {
        if (issue == null)
        {
            WriteWarning("A null issue was passed.", log);
            return false;
        }

        WriteTrace($"Issue {issue.Number} has labels {string.Join(", ", issue.Labels.Select(l => $"'{l.Name}'"))}", log, logLevel);

        bool isIssueLabel = false;

        if (issueLabel == GithubLabelNames.IssueLabel)
        {
            isIssueLabel = issue.Labels.Any(l => l.Name == repoLabel)
                           && !issue.Labels.Any(l => l.Name == GithubLabelNames.HotfixLabel ||
                                                     l.Name == GithubLabelNames.RollbackLabel ||
                                                     l.Name == GithubLabelNames.DowntimeLabel ||
                                                     l.Name == GithubLabelNames.FailureLabel);
        }
        else
        {
            isIssueLabel = issue.Labels.Any(l => l.Name == issueLabel) && issue.Labels.Any(l => l.Name == repoLabel);
        }

        if (isIssueLabel)
        {
            WriteDebug($"Issue {issue.Number} determined to be {issueLabel} for {repoLabel}", log, logLevel);
        }

        return isIssueLabel;
    }

    public static string HandleApiRedirect(HttpResponseMessage redirect, Uri apiRequest, ILogger log = null)
    {
        // Since the API will sometimes 302 us, we're going to do a quick check to see
        // that we're still being sent to AzDO and not some random location
        // If so, we'll provide our auth so we don't get 401'd
        Uri redirectUri = redirect.Headers.Location;
        if (redirectUri.Scheme.ToLower() != "https")
        {
            WriteError($"API attempted to redirect to using incorrect scheme (expected 'https', was '{redirectUri.Scheme}'", log);
            WriteError($"Request URI: '{apiRequest}'\nRedirect URI: '{redirectUri}'", log);
            throw new HttpRequestException("Bad redirect scheme");
        }
        else if (redirectUri.Host != apiRequest.Host)
        {
            WriteError($"API attempted to redirect to unknown host '{redirectUri.Host}' (expected '{apiRequest.Host}'); not passing auth parameters", log);
            WriteError($"Request URI: '{apiRequest}'\nRedirect URI: '{redirectUri}'", log);
            throw new HttpRequestException("Bad redirect host");
        }
        else
        {
            return redirectUri.ToString();
        }
    }
    public static TableClient GetTableClient(string storageAccount, string tableName)
    {
        return new TableClient(new Uri($"https://{storageAccount}.table.core.windows.net"), tableName, new DefaultAzureCredential()); ;
    }

    public static GitHubClient GetGithubClient(string githubPat)
    {
        ProductInfoHeaderValue productHeader = Program.GetProductInfoHeaderValue();
        GitHubClient githubClient = new GitHubClient(new Octokit.ProductHeaderValue(productHeader.Product.Name, productHeader.Product.Version))
        {
            Credentials = new Credentials("fake", githubPat)
        };

        return githubClient;
    }

    public static void WriteError(string message, ILogger log = null)
    {
        message = $"ERROR: {message}";
        if (log == null)
        {
            WriteColoredMessage(message, ConsoleColor.Red);
        }
        else
        {
            log.LogError(message);
        }
    }

    public static void WriteWarning(string message, ILogger log)
    {
        message = $"WARNING: {message}";
        if (log == null)
        {
            WriteColoredMessage(message, ConsoleColor.Yellow);
        }
        else
        {
            log.LogWarning(message);
        }
    }
    public static void WriteInformation(string message, ILogger log)
    {
        message = $"INFO: {message}";
        if (log == null)
        {
            WriteColoredMessage(message, ConsoleColor.White);
        }
        else
        {
            log.LogInformation(message);
        }
    }

    public static void WriteDebug(string message, ILogger log, Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        message = $"DEBUG: {message}";
        if (log != null)
        {
            log.LogInformation(message);
        }
        else if (logLevel <= Microsoft.Extensions.Logging.LogLevel.Debug)
        {
            WriteColoredMessage(message, ConsoleColor.Gray);
        }    
    }

    public static void WriteTrace(string message, ILogger log, Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        message = $"TRACE: {message}";
        if (log != null)
        {
            log.LogInformation(message);
        }
        else if (logLevel <= Microsoft.Extensions.Logging.LogLevel.Trace)
        {
            WriteColoredMessage(message, ConsoleColor.DarkGray);
        }
    }

    private static void WriteColoredMessage(string message, ConsoleColor textColor)
    {
        ConsoleColor currentTextColor = Console.ForegroundColor;
        Console.ForegroundColor = textColor;
        Console.WriteLine(message);
        Console.ForegroundColor = currentTextColor;
    }
}

public static class AzureDevOpsCommitTags
{
    public const string RolloutTag = "[ROLLOUT]";
    public const string HotfixTag = "[HOTFIX]";
    public const string RollbackTag = "[ROLLBACK]";
}

public static class GithubLabelNames
{
    public const string IssueLabel = "Rollout Issue";
    public const string HotfixLabel = "Rollout Manual Hotfix";
    public const string RollbackLabel = "Rollout Manual Rollback";
    public const string DowntimeLabel = "Rollout Downtime";
    public const string FailureLabel = "Rollout Failure";
}

public static class ScorecardsStorageAccount
{
    public static string Name = 
        Environment.GetEnvironmentVariable("ScorecardsStorageAccountName") ?? "rolloutscorecards";
    public static string ScorecardsTableName = 
        Environment.GetEnvironmentVariable("ScorecardsStorageAccountTableName") ?? "scorecards";
}

public static class DeploymentsStorageAccount
{
    public static string Name =
        Environment.GetEnvironmentVariable("DeploymentsStorageAccountName") ?? "dotnetengstatusprod";
    public static string DeploymentsTableName =
        Environment.GetEnvironmentVariable("DeploymentsTableName") ?? "deployments";
}
