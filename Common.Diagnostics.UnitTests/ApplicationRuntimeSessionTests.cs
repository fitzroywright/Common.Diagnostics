namespace Common.Diagnostics.UnitTests;

public sealed class ApplicationRuntimeSessionTests
{
    [Fact]
    public void Requests_share_one_process_id_and_do_not_accumulate_session_headers()
    {
        string first = ApplicationRuntimeSession.Id;
        Assert.True(Guid.TryParseExact(first, "N", out _));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test");
        request.Headers.TryAddWithoutValidation(ApplicationRuntimeSession.HeaderName, "stale-session");
        ApplicationRuntimeSession.Apply(request);
        ApplicationRuntimeSession.Apply(request);
        using var another = new HttpRequestMessage(HttpMethod.Post, "https://example.test");
        ApplicationRuntimeSession.Apply(another);
        Assert.Equal(first, Assert.Single(request.Headers.GetValues(ApplicationRuntimeSession.HeaderName)));
        Assert.Equal(first, Assert.Single(another.Headers.GetValues(ApplicationRuntimeSession.HeaderName)));
    }
}
