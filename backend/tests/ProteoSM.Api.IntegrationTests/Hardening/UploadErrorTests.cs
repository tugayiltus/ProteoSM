using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ProteoSM.Api.Http;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Api.IntegrationTests.Tickets;

namespace ProteoSM.Api.IntegrationTests.Hardening;

[Collection(ApiCollection.Name)]
public sealed class UploadErrorTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_truncated_form_is_a_bad_request()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        using var content = new ByteArrayContent(Encoding.ASCII.GetBytes("--b\r\nContent-Disposition: form-data; name=\"body\"\r\n\r\nMer"));
        content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=b");

        using var truncated = await world.Employee.SendContentAsync(TicketWorld.PathOf(incident, "comments"), content);

        Assert.Equal(HttpStatusCode.BadRequest, truncated.StatusCode);
    }

    [Fact]
    public async Task A_body_over_the_limit_is_413()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        await handler.TryHandleAsync(context, new BadHttpRequestException("Too large.", StatusCodes.Status413PayloadTooLarge), Ct);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
    }
}
