using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Sitecore.Commerce.Core;
using Sitecore.Framework.Conditions;
using Sitecore.Framework.Pipelines;

namespace Custom.Plugin.Sample.CI
{
    [PipelineDisplayName("Core.block.ValidateEnvironmentJson")]
    public class CustomValidateEnvironmentJsonBlock : PipelineBlock<string, CommerceEnvironment, CommercePipelineExecutionContext>
    {
        public override async Task<CommerceEnvironment> Run(string arg, CommercePipelineExecutionContext context)
        {
            CustomValidateEnvironmentJsonBlock environmentJsonBlock = this;
            Condition.Requires<string>(arg).IsNotNullOrEmpty(environmentJsonBlock.Name + ": The raw environment cannot be null or empty.");
            int num = 0;
            CommerceEnvironment commerceEnvironment = null;
            Exception exception = null;

            try
            {
                commerceEnvironment = JsonConvert.DeserializeObject<CommerceEnvironment>(arg, new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    NullValueHandling = NullValueHandling.Ignore
                });
            }
            catch (Exception ex)
            {
                num = 1;
                exception = ex;
            }
            if (num != 1)
            {
                return commerceEnvironment;
            }
           
            CommercePipelineExecutionContext executionContext = context;
            CommerceContext commerceContext = context.CommerceContext;
            string error = context.GetPolicy<KnownResultCodes>().Error;
            string commerceTermKey = "InvalidEnvironmentJson";
            object[] args = new object[1] { (object)exception };
            string defaultMessage = "Environment json is not valid.";
            executionContext.Abort(await commerceContext.AddMessage(error, commerceTermKey, args, defaultMessage).ConfigureAwait(false), (object)context);
            executionContext = null;
            return null;
        }
    }
}
