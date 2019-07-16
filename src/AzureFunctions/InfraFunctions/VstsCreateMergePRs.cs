using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace InfraFunctions
{
    public static class VstsCreateMergePRs
    {
        [FunctionName("VstsCreateMergePRs")]
        public static void Run([TimerTrigger("0 0 */12 * * *")]TimerInfo myTimer, ILogger log, ExecutionContext context)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            RunAsync(log, context).GetAwaiter().GetResult();
        }

        private static async Task MakeVstsPr(ILogger log, string repoName, string srcBranch, string destBranch)
        {
            log.LogInformation($"Merging {repoName} from {srcBranch} to {destBranch}");

            var vstsInitializer = new VstsMergeTool.Initializer(srcBranch, destBranch);

            var (prCreated, errorMessage) = await vstsInitializer.MergeTool.CreatePullRequest();

            if (prCreated)
            {
                log.LogInformation("PR created successfully");
            }
            else if (errorMessage == null)
            {
                log.LogInformation("PR creation skipped. PR already exists or all commits are present in base branch.");
            }
            else
            {
                log.LogError($"Could not create PR. {errorMessage}");
            }
        }

        private static async Task RunAsync(ILogger log, ExecutionContext context)
        {
            var configPath = Path.Combine(context.FunctionDirectory, "VstsPrConfig.xml");
            var config = XDocument.Load(configPath).Root;
            foreach (var repo in config.Elements("repo"))
            {
                var repoName = repo.Attribute("name").Value;
                foreach (var merge in repo.Elements("merge"))
                {
                    var fromBranch = merge.Attribute("from").Value;
                    var toBranch = merge.Attribute("to").Value;
                    await MakeVstsPr(log, repoName, fromBranch, toBranch);
                }
            }
        }
    }
}
