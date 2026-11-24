using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Proteone.Api.OpenApi;

/// <summary>
/// A property marked <c>[JsonIgnore(Condition = WhenWritingNull)]</c> is left out of the JSON when it is null, so the
/// contract must not list it as required: the generated client type then reads <c>prop?: T | null</c> and a strict
/// <c>=== null</c> check cannot silently miss an absent key (e.g. <c>TicketDetail.service</c>, Phase 12).
/// </summary>
internal sealed class OmittedWhenNullSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Required is not { Count: > 0 } required)
        {
            return Task.CompletedTask;
        }

        foreach (var property in context.JsonTypeInfo.Properties)
        {
            var ignore = property.AttributeProvider?.GetCustomAttributes(typeof(JsonIgnoreAttribute), inherit: true)
                .OfType<JsonIgnoreAttribute>().FirstOrDefault();
            if (ignore?.Condition == JsonIgnoreCondition.WhenWritingNull)
            {
                required.Remove(property.Name);
            }
        }

        return Task.CompletedTask;
    }
}
