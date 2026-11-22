namespace CollectPlayAchieve.Api.Infrastructure;

internal static class EndpointExtensions
{
    extension(WebApplication app)
    {
        public void MapEndpoints()
        {
            foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
            {
                endpoint.MapEndpoint(app);
            }
        }
    }
}
