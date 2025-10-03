using LLMGateway.Application.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LLMGateway.Application.Orchestration;

public class KernelFactory
{
    private readonly IServiceProvider _serviceProvider;

    public KernelFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Kernel CreateWithPlugins()
    {
        var builder = Kernel.CreateBuilder();

        // Register chat completion service from DI
        var chatCompletionService = _serviceProvider.GetRequiredService<IChatCompletionService>();
        builder.Services.AddSingleton(chatCompletionService);

        // Register plugins from DI (with resolved dependencies)
        var modelSelection = _serviceProvider.GetRequiredService<ModelSelectionPlugin>();
        builder.Plugins.AddFromObject(modelSelection, pluginName: "ModelSelection");

        var costTracking = _serviceProvider.GetRequiredService<CostTrackingPlugin>();
        builder.Plugins.AddFromObject(costTracking, pluginName: "CostTracking");

        var providerFallback = _serviceProvider.GetRequiredService<ProviderFallbackPlugin>();
        builder.Plugins.AddFromObject(providerFallback, pluginName: "ProviderFallback");

        return builder.Build();
    }
}