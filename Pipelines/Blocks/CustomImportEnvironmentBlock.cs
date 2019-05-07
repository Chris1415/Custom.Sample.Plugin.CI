using Sitecore.Commerce.Core;
using Sitecore.Framework.Conditions;
using Sitecore.Framework.Pipelines;
using System;
using System.Threading.Tasks;

namespace Custom.Plugin.Sample.CI
{
    [PipelineDisplayName("Core.block.CustomImportEnvironment")]
    public class CustomImportEnvironmentBlock : PipelineBlock<CommerceEnvironment, CommerceEnvironment, CommercePipelineExecutionContext>
    {
        private readonly IFindEntityPipeline _findEntityPipeline;

        public CustomImportEnvironmentBlock(IFindEntityPipeline findEntityPipeline)
          : base((string)null)
        {
            this._findEntityPipeline = findEntityPipeline;
        }

        public override async Task<CommerceEnvironment> Run(CommerceEnvironment arg, CommercePipelineExecutionContext context)
        {
            CustomImportEnvironmentBlock environmentBlock = this;
            Condition.Requires(arg).IsNotNull(environmentBlock.Name + ": The argument cannot be null.");
            arg.IsPersisted = false;
            arg.Id = CommerceEntity.IdPrefix<CommerceEnvironment>() + arg.Name;
            arg.EntityVersion = 1;
            arg.Published = true;
            if (arg.ArtifactStoreId == Guid.Empty)
            {
                arg.ArtifactStoreId = Guid.NewGuid();
            }

            CommerceEnvironment commerceEnvironment = await environmentBlock._findEntityPipeline.Run(new FindEntityArgument(typeof(CommerceEnvironment), arg.Id, false), context).ConfigureAwait(false) as CommerceEnvironment;
            if (commerceEnvironment != null)
            {
                arg.IsPersisted = true;
                arg.Version = commerceEnvironment.Version;
            }

            return arg;
        }
    }
}
