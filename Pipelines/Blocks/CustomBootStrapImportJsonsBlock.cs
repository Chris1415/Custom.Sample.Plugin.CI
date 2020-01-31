using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.Core.Commands;
using Sitecore.Framework.Conditions;
using Sitecore.Framework.Pipelines;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Plugin.Alnatura.CI
{
    [PipelineDisplayName("Core.block.CustomBootStrapImportJsonsBlock")]
    public class CustomBootStrapImportJsonsBlock : PipelineBlock<string, string, CommercePipelineExecutionContext>
    {
        private readonly NodeContext _nodeContext;
        private readonly ImportEnvironmentCommand _importEnvironmentCommand;
        private readonly ImportPolicySetCommand _importPolicySetCommand;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly bool ReplaceWithEnvironmentSpecificFile = true;

        public CustomBootStrapImportJsonsBlock(NodeContext nodeContext,
            ImportEnvironmentCommand importEnvironmentCommand,
            ImportPolicySetCommand importPolicySetCommand,
            IHostingEnvironment hostingEnvironment)
        {
            this._nodeContext = nodeContext;
            this._importEnvironmentCommand = importEnvironmentCommand;
            this._importPolicySetCommand = importPolicySetCommand;
            this._hostingEnvironment = hostingEnvironment;
        }

        public override async Task<string> Run(string arg, CommercePipelineExecutionContext context)
        {
            CustomBootStrapImportJsonsBlock importJsonsBlock = this;
            string environmentName = this._hostingEnvironment.EnvironmentName;
            Condition.Requires(arg).IsNotNull(importJsonsBlock.Name + ": The argument cannot be null.");
            string[] otherEnvironments = Directory.GetFiles(importJsonsBlock._nodeContext.WebRootPath + "\\data\\environments", $"*.json");

            for (int index = 0; index < otherEnvironments.Length; ++index)
            {
                string fileName = otherEnvironments[index];
                string file = File.ReadAllText(fileName);

                string environmentSpecificFileName = $"{fileName.Substring(0, fileName.LastIndexOf('.'))}.{environmentName}.json";
                string environmentSpecificFile;
                try
                {
                    environmentSpecificFile = File.ReadAllText(environmentSpecificFileName);
                }
                catch (Exception)
                {
                    environmentSpecificFile = null;
                }

                bool skipFile = false;
                foreach (string otherEnvironment in otherEnvironments)
                {
                    string brokenFileNameCurrentFile = $"{fileName.Substring(0, fileName.LastIndexOf('.'))}";
                    brokenFileNameCurrentFile = $"{brokenFileNameCurrentFile.Substring(0, brokenFileNameCurrentFile.LastIndexOf('.'))}";
                    string brokenFileNameFromList = $"{otherEnvironment.Substring(0, otherEnvironment.LastIndexOf('.'))}";
                    if (brokenFileNameFromList.Equals(brokenFileNameCurrentFile))
                    {
                        skipFile = true;
                    }
                }

                if (skipFile)
                {
                    continue;
                }

                var targetFileToUse = ReplaceWithEnvironmentSpecificFile ? environmentSpecificFile ?? file : file;
                JObject jobject = JObject.Parse(targetFileToUse);

                // Json Validation if any property is present
                if (!jobject.HasValues || !jobject.Properties().Any(p => p.Name.Equals("$type", StringComparison.OrdinalIgnoreCase)))
                {
                    context.Logger.LogError(importJsonsBlock.Name + ".Invalid json file '" + targetFileToUse + "'.", Array.Empty<object>());
                    break;
                }

                JProperty jproperty = jobject.Properties().FirstOrDefault(p => p.Name.Equals("$type", StringComparison.OrdinalIgnoreCase));
                // Json Validation if type property is present
                if (string.IsNullOrEmpty(jproperty?.Value?.ToString()))
                {
                    context.Logger.LogError(importJsonsBlock.Name + ".Invalid type in json file '" + targetFileToUse + "'.", Array.Empty<object>());
                    break;
                }

                // In case we have an environment specific json -> overwrite the given properties in the original json
                if (!ReplaceWithEnvironmentSpecificFile && !string.IsNullOrEmpty(environmentSpecificFile))
                {
                    JObject environmentspecificJObject = JObject.Parse(environmentSpecificFile);
                    JProperty specificProperty = environmentspecificJObject.Properties().FirstOrDefault();
                    IterateJsonProperties(ref jobject, specificProperty as JToken);
                    targetFileToUse = jobject.ToString();
                }

                // Determination if file is environment 
                if (jproperty.Value.ToString().Contains(typeof(CommerceEnvironment).FullName))
                {
                    context.Logger.LogInformation(importJsonsBlock.Name + ".ImportEnvironmentFromFile: File=" + targetFileToUse, Array.Empty<object>());
                    try
                    {
                        CommerceEnvironment commerceEnvironment = await importJsonsBlock._importEnvironmentCommand.Process(context.CommerceContext, targetFileToUse);
                        context.Logger.LogInformation(importJsonsBlock.Name + ".EnvironmentImported: EnvironmentId=" + commerceEnvironment.Id + "|File=" + targetFileToUse, Array.Empty<object>());
                    }
                    catch (Exception ex)
                    {
                        context.CommerceContext.LogException(importJsonsBlock.Name + ".ImportEnvironmentFromFile", ex);
                    }
                }
                // Or policy-set
                else if (jproperty.Value.ToString().Contains(typeof(PolicySet).FullName))
                {
                    context.Logger.LogInformation(importJsonsBlock.Name + ".ImportPolicySetFromFile: File=" + targetFileToUse, Array.Empty<object>());
                    try
                    {
                        PolicySet policySet = await importJsonsBlock._importPolicySetCommand.Process(context.CommerceContext, targetFileToUse);
                        context.Logger.LogInformation(importJsonsBlock.Name + ".PolicySetImported: PolicySetId=" + policySet.Id + "|File=" + targetFileToUse, Array.Empty<object>());
                    }
                    catch (Exception ex)
                    {
                        context.CommerceContext.LogException(importJsonsBlock.Name + ".ImportPolicySetFromFile", ex);
                    }
                }
            }

            return arg;
        }

        private void IterateJsonProperties(ref JObject rootOrigin, JToken environmentSpecific)
        {
            if (environmentSpecific == null || rootOrigin == null)
            {
                return;
            }

            bool skipChildren = false;
            if (environmentSpecific is JValue)
            {
                skipChildren = true;
            }

            if (environmentSpecific is JObject)
            {
                var typeChild = environmentSpecific["$type"];
                if (typeChild != null && !typeChild.ToString().Equals("System.Collections.Generic.List`1[[Sitecore.Commerce.Core.Policy, Sitecore.Commerce.Core]], mscorlib"))
                {
                    JToken matchingOriginElement = FindMatchingElement(rootOrigin, typeChild);
                    JObject originObject = matchingOriginElement.Parent.Parent as JObject;
                    originObject.Merge(environmentSpecific, new JsonMergeSettings()
                    {
                        MergeArrayHandling = MergeArrayHandling.Merge
                    });
                    skipChildren = true;
                }
            }

            if (!skipChildren)
            {
                JToken specificChild = environmentSpecific.Children().Any() ? environmentSpecific.First() : null;
                if (specificChild != null)
                {
                    IterateJsonProperties(ref rootOrigin, specificChild);
                }
            }

            JToken specificNext = environmentSpecific.Next;
            if (specificNext != null)
            {
                IterateJsonProperties(ref rootOrigin, specificNext);
            }
        }

        private JToken FindMatchingElement(JToken rootOrigin, JToken targetElement)
        {
            if (rootOrigin == null)
            {
                return null;
            }

            foreach (var element in rootOrigin)
            {
                var typeElement = element is JObject ? element["$type"] : null;
                if (typeElement != null
                    && typeElement.ToString().Equals(targetElement.ToString()))
                {
                    return typeElement;
                }

                var foundElement = FindMatchingElement(element, targetElement);
                if (foundElement != null)
                {
                    return foundElement;
                }
            }

            return null;
        }
    }
}
