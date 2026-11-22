using Microsoft.AspNetCore.Routing.Patterns;

namespace CollectPlayAchieve.Api.Tests;

internal static class RoutePatternExtensions
{
    extension(RoutePattern pattern)
    {
        public string ToOpenApiPath() =>
            "/"
            + string.Join(
                "/",
                pattern.PathSegments.Select(segment =>
                    string.Concat(
                        segment.Parts.Select(part =>
                            part switch
                            {
                                RoutePatternLiteralPart literal => literal.Content,
                                RoutePatternSeparatorPart separator => separator.Content,
                                RoutePatternParameterPart parameter => $"{{{parameter.Name}}}",
                                _ => throw new InvalidOperationException(
                                    $"Unsupported route part in '{pattern.RawText}'."
                                ),
                            }
                        )
                    )
                )
            );
    }
}
