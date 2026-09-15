using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Common.Registration;

namespace Common.Diagnostics.UnitTests;

public sealed class RegistrationContractTests
{
    [Fact]
    public void MetadataOnly_RemovesValues_ButPreservesRequirementEvidence()
    {
        JsonObject contract = new()
        {
            ["applicationId"] = "Aegis.Cafeteria.Services",
            ["requirements"] = new JsonArray
            {
                new JsonObject
                {
                    ["key"] = "FfpIntegration:Server",
                    ["required"] = true,
                    ["isConfigured"] = true,
                    ["hasDefault"] = true,
                    ["effectiveSource"] = "Environment Variable (FfpIntegration__Server)",
                    ["value"] = "sql01",
                    ["currentValue"] = "sql01",
                    ["resolvedValue"] = "sql01",
                    ["effectiveValue"] = "sql01",
                    ["safeDisplayValue"] = "sql01",
                    ["defaultValue"] = "sql-default",
                    ["example"] = "sql-example"
                }
            }
        };

        JsonObject sanitized = ConfigurationContractPolicy.MetadataOnly(contract);
        JsonObject requirement = sanitized["requirements"]!.AsArray()[0]!.AsObject();

        Assert.Equal("FfpIntegration:Server", requirement["key"]!.GetValue<string>());
        Assert.True(requirement["required"]!.GetValue<bool>());
        Assert.True(requirement["isConfigured"]!.GetValue<bool>());
        Assert.True(requirement["hasDefault"]!.GetValue<bool>());
        Assert.Equal("Environment Variable (FfpIntegration__Server)", requirement["effectiveSource"]!.GetValue<string>());

        foreach (string field in new[] { "value", "currentValue", "resolvedValue", "effectiveValue", "safeDisplayValue", "defaultValue", "example" })
            Assert.False(requirement.ContainsKey(field));

        Assert.Equal("sql01", contract["requirements"]!.AsArray()[0]!["value"]!.GetValue<string>());
    }

    [Fact]
    public void MetadataOnly_RemovesValueBearingFields_Recursively()
    {
        JsonObject contract = new()
        {
            ["applicationId"] = "Future.App",
            ["metadata"] = new JsonObject
            {
                ["purpose"] = "Keep this metadata",
                ["defaultValue"] = "must-not-leave-process",
                ["nested"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "Nested requirement evidence",
                        ["value"] = "secret-ish",
                        ["isConfigured"] = true
                    }
                }
            }
        };

        JsonObject sanitized = ConfigurationContractPolicy.MetadataOnly(contract);
        JsonObject metadata = sanitized["metadata"]!.AsObject();
        JsonObject nested = metadata["nested"]!.AsArray()[0]!.AsObject();

        Assert.Equal("Keep this metadata", metadata["purpose"]!.GetValue<string>());
        Assert.False(metadata.ContainsKey("defaultValue"));
        Assert.Equal("Nested requirement evidence", nested["name"]!.GetValue<string>());
        Assert.True(nested["isConfigured"]!.GetValue<bool>());
        Assert.False(nested.ContainsKey("value"));

        Assert.Equal("must-not-leave-process", contract["metadata"]!["defaultValue"]!.GetValue<string>());
        Assert.Equal("secret-ish", contract["metadata"]!["nested"]!.AsArray()[0]!["value"]!.GetValue<string>());
    }

    [Fact]
    public async Task RegisterContractAsync_SendsMetadataOnlyPayload()
    {
        string? body = null;
        var handler = new RecordingHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpClient(handler);
        var registrar = new ConfigurationRegistrar(client, new RegistrationOptions(new Uri("https://configuration.example/"), "test-key"));
        JsonObject contract = new()
        {
            ["requirements"] = new JsonArray
            {
                new JsonObject
                {
                    ["key"] = "ConnectionString",
                    ["isConfigured"] = true,
                    ["safeDisplayValue"] = "secret-ish",
                    ["defaultValue"] = "fallback"
                }
            }
        };

        RegistrationResult result = await registrar.RegisterContractAsync(contract);

        Assert.True(result.Succeeded);
        Assert.NotNull(body);
        Assert.DoesNotContain("secret-ish", body, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback", body, StringComparison.Ordinal);
        Assert.Contains("ConnectionString", body, StringComparison.Ordinal);
        Assert.Contains("isConfigured", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterContractAsync_DoesNotReportSuccess_WhenConfigurationRejectsRequest()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = new HttpClient(handler);
        var registrar = new ConfigurationRegistrar(client, new RegistrationOptions(new Uri("https://configuration.example/"), "test-key"));

        RegistrationResult result = await registrar.RegisterContractAsync(new JsonObject());

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        Assert.Contains("HTTP 503", result.Error, StringComparison.Ordinal);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
