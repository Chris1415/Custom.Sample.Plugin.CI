using Sitecore.Commerce.Core;
using Sitecore.Commerce.Core.Commands;
using Sitecore.Framework.Conditions;
using Sitecore.Framework.Pipelines;
using System.Threading.Tasks;

namespace Custom.Plugin.Sample.CI
{
    [PipelineDisplayName("Core.block.CustomPersistEnvironment")]
    public class CustomPersistEnvironmentBlock : PipelineBlock<CommerceEnvironment, CommerceEnvironment, CommercePipelineExecutionContext>
    {
        private readonly IPersistEntityPipeline _persistEntityPipeline;
        private readonly ResetNodeContextCommand _resetNodeContext;

        public CustomPersistEnvironmentBlock(IPersistEntityPipeline persistEntityPipeline, ResetNodeContextCommand resetNodeContextCommand)
          : base(null)
        {
            this._persistEntityPipeline = persistEntityPipeline;
            this._resetNodeContext = resetNodeContextCommand;
        }

        public override async Task<CommerceEnvironment> Run(CommerceEnvironment arg, CommercePipelineExecutionContext context)
        {
            CustomPersistEnvironmentBlock environmentBlock = this;
            Condition.Requires(arg).IsNotNull(environmentBlock.Name + ": The argument cannot be null.");
            arg = (await environmentBlock._persistEntityPipeline.Run(new PersistEntityArgument(arg), context).ConfigureAwait(false)).Entity as CommerceEnvironment;
            if (arg != null)
            {
                CommerceContext commerceContext = context.CommerceContext;
                ImportedEnvironmentModel environmentModel = new ImportedEnvironmentModel(arg.Id);
                environmentModel.Name = arg.Name;
                commerceContext.AddModel(environmentModel);
                int num = await environmentBlock._resetNodeContext.Process(context.CommerceContext, arg.Id).ConfigureAwait(false) ? 1 : 0;
            }

            return arg;
        }
    }
}
