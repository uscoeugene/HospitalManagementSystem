using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HMS.API.Infrastructure.Swagger
{
    public class SanitizeResponseContentOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // no-op placeholder to avoid affecting generated OpenAPI
        }
    }
}
