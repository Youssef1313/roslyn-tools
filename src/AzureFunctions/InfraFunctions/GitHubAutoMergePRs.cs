using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace InfraFunctions
{
    public static class GitHubAutoMergePRs
    {
        [FunctionName("GithubAutoMergePRs")]
        public static void Run([TimerTrigger("0 */30 * * * *")]TimerInfo myTimer, ILogger log, ExecutionContext context)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }

        private static async Task RunAsync(ILogger log, ExecutionContext context)
        {
            var gh = new GithubMergeTool.GithubMergeTool("dotnet-automerge-bot@users.noreply.github.com", await AuthHelpers.GetSecret("dotnet-automerge-bot-token"));
            var configPath = Path.Combine(context.FunctionDirectory, "config.xml");
            var config = XDocument.Load(configPath).Root;
            foreach (var repo in config.Elements("repo"))
            {
                var owner = repo.Attribute("owner").Value;
                var name = repo.Attribute("name").Value;
                var (autoMergeablePrs, error) = await gh.FetchAutoMergeablePrs(owner, name);
                if (error != null)
                {
                    log.LogError($"Unable to fetch auto-mergeable PRs from '{owner}/{name}': {error.Content}");
                    continue;
                }

                foreach (var pr in autoMergeablePrs)
                {
                    var prIdentifier = $"{owner}/{name}:{pr}";
                    log.LogInformation("Checking " + prIdentifier);
                    try
                    {
                        var (merged, message, mergeError) = await gh.MergeAutoMergeablePr(owner, name, pr);

                        if (merged)
                        {
                            log.LogInformation($"Auto-merged PR '{prIdentifier}'.");
                        }
                        else if (message != null)
                        {
                            log.LogInformation($"PR '{prIdentifier}' not a candidate for auto-merging: {message}");
                        }
                        else if (mergeError != null)
                        {
                            log.LogError($"Unable to auto-merge PR '{prIdentifier}': {await mergeError.Content.ReadAsStringAsync()}");
                        }
                        else
                        {
                            log.LogError($"Unable to auto-merg PR '{prIdentifier}' for unknown reason.");
                        }
                    }
                    catch (Exception ex)
                    {
                        // If a specific merge fails, we don't want to fail all merges. Log the exception
                        // and continue trying to merge other PRs 
                        log.LogError(ex);
                    }

                    // Delay in order to avoid triggering GitHub rate limiting
                    await Task.Delay(5000);
                }
            }
        }
    }
}
