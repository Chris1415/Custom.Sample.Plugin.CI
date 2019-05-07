namespace Custom.Plugin.Sample.CI
{
    using System.Reflection;
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore.Commerce.Core;
    using Sitecore.Framework.Configuration;
    using Sitecore.Framework.Pipelines.Definitions.Extensions;

    /// <summary>
    /// The configure sitecore class.
    /// </summary>
    public class ConfigureSitecore : IConfigureSitecore
    {
        /// <summary>
        /// The configure services.
        /// </summary>
        /// <param name="services">
        /// The services.
        /// </param>
        public void ConfigureServices(IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();
            services.RegisterAllPipelineBlocks(assembly);

            services.Sitecore().Pipelines(config => config
            .ConfigurePipeline<IImportEnvironmentPipeline>(
                configure =>
                { 
                    configure.Replace<ImportEnvironmentBlock, CustomImportEnvironmentBlock>()
                             .Replace<ValidateEnvironmentJsonBlock, CustomValidateEnvironmentJsonBlock>()
                             .Replace<PersistEnvironmentBlock, CustomPersistEnvironmentBlock>();
                })
             .ConfigurePipeline<IBootstrapPipeline>(
                configure =>
                {
                    configure.Replace<BootStrapImportJsonsBlock, CustomBootStrapImportJsonsBlock>();
                }));

            services.RegisterAllCommands(assembly);
        }
    }
}