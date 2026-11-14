namespace Levvy.Api.Endpoints;

/// <summary>
/// Serves interactive Swagger UI backed by the ASP.NET Core OpenAPI document at /openapi/v1.json.
/// </summary>
public static class SwaggerEndpoints
{
    private const string SwaggerHtml = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>Levvy API — Swagger UI</title>
          <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css" />
          <style>
            body { margin: 0; background: #fafafa; }
          </style>
        </head>
        <body>
          <div id="swagger-ui"></div>
          <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
          <script>
            window.ui = SwaggerUIBundle({
              url: '/openapi/v1.json',
              dom_id: '#swagger-ui',
              deepLinking: true,
              presets: [SwaggerUIBundle.presets.apis]
            });
          </script>
        </body>
        </html>
        """;

    public static IEndpointRouteBuilder MapSwaggerEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/swagger", () => Results.Content(SwaggerHtml, "text/html"))
              .ExcludeFromDescription();

        return routes;
    }
}
