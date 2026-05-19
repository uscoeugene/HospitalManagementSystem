using System.Collections.Generic;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HMS.API.Swagger
{
    public class SwaggerAuthOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
                operation.Parameters = new List<OpenApiParameter>();

            // optional tenant header for testing
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Tenant-Id",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Optional tenant id for testing"
            });

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Tenant-Code",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Optional tenant code for testing"
            });
        }
    }
}
