using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace InfraFunctions
{
    public static class GitHubCreateMergePRFunction
    {
        [FunctionName("GitHubCreateMergePRFunction")]
        public static void Run([TimerTrigger("0 */30 * * * *")]TimerInfo myTimer, ILogger log, ExecutionContext context)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var automatedRunStartTime = myTimer.ScheduleStatus.Next;
            var isAutomatedRun = DateTime.Now >= automatedRunStartTime;

            RunAsync(log, context, isAutomatedRun).GetAwaiter().GetResult();
        }


        private static async Task MakeGithubPr(
            GithubMergeTool.GithubMergeTool gh,
            string repoOwner,
            string repoName,
            string srcBranch,
            string destBranch,
            bool addAutoMergeLabel,
            bool isAutomatedRun,
            ILogger log)
        {
            log.LogInformation($"Merging {repoName} from {srcBranch} to {destBranch}");

            var (prCreated, error) = await gh.CreateMergePr(repoOwner, repoName, srcBranch, destBranch, addAutoMergeLabel, isAutomatedRun);

            if (prCreated)
            {
                log.LogInformation("PR created successfully");
            }
            else if (error == null)
            {
                log.LogInformation("PR creation skipped. PR already exists or all commits are present in base branch");
            }
            else
            {
                log.LogError($"Error creating PR. GH response code: {error.StatusCode}");
                log.LogError(await error.Content.ReadAsStringAsync());
            }
        }

        private static async Task RunAsync(ILogger log, ExecutionContext context, bool isAutomatedRun)
        {
            var gh = new GithubMergeTool.GithubMergeTool("dotnet-bot@users.noreply.github.com", await AuthHelpers.GetSecret("dotnet-bot-github-auth-token"));
            var configPath = Path.Combine(context.FunctionDirectory, "config.xml");
            var config = XDocument.Load(configPath).Root;
            foreach (var repo in config.Elements("repo"))
            {
                var owner = repo.Attribute("owner").Value;
                var name = repo.Attribute("name").Value;
                foreach (var merge in repo.Elements("merge"))
                {
                    var fromBranch = merge.Attribute("from").Value;
                    var toBranch = merge.Attribute("to").Value;

                    var frequency = merge.Attribute("frequency")?.Value;
                    if (isAutomatedRun && frequency == "weekly" && DateTime.Now.DayOfWeek != DayOfWeek.Sunday)
                    {
                        continue;
                    }

                    var addAutoMergeLabel = bool.Parse(merge.Attribute("addAutoMergeLabel")?.Value ?? "true");
                    await MakeGithubPr(gh, owner, name, fromBranch, toBranch, addAutoMergeLabel, isAutomatedRun, log);

                    // Delay in order to avoid triggering GitHub rate limiting
                    await Task.Delay(4000);
                }
            }
        }
    }
}
