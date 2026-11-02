namespace AndEth3D.Backend.Services;

public static class ApplicationServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<EstimationService>();
        services.AddScoped<ParametricService>();
        services.AddScoped<OperationsService>();
        services.AddScoped<ChatService>();
        services.AddScoped<GalleryService>();
        services.AddScoped<ManufacturingService>();
        services.AddScoped<OrdersService>();
        services.AddScoped<QuotesService>();
        services.AddScoped<VisualProposalsService>();
        services.AddScoped<VisualSettingsService>();
        return services;
    }
}
