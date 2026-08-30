using System.Diagnostics;
using Adesha.ServiceDefaults.Redaction;

namespace Adesha.ServiceDefaults.Tests;

/// <summary>
/// Rule 3: no span attribute may contain a credential. HttpClient instrumentation renders
/// outbound request detail (including the m.Stock "token api_key:jwt" Authorization header)
/// into span attributes, so redaction must happen at the span level before export.
/// </summary>
public class OtelSpanRedactionTests
{
    private const string FakeJwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0In0.c2lnbmF0dXJl";

    private static Activity ProcessedActivity(params (string Key, string Value)[] tags)
    {
        var activity = new Activity("test-span");
        foreach (var (key, value) in tags)
        {
            activity.SetTag(key, value);
        }

        activity.Start();
        activity.Stop();
        new CredentialRedactionProcessor().OnEnd(activity);
        return activity;
    }

    [Theory]
    [InlineData("http.request.header.authorization")]
    [InlineData("api_key")]
    [InlineData("broker.access_token")]
    [InlineData("password")]
    public void Sensitive_attribute_names_are_redacted(string tagKey)
    {
        var activity = ProcessedActivity((tagKey, "some-credential-value"));

        Assert.Equal(CredentialRedactor.RedactedValue, activity.GetTagItem(tagKey));
    }

    [Fact]
    public void Credential_shaped_values_are_redacted_regardless_of_attribute_name()
    {
        var activity = ProcessedActivity(
            ("url.full", $"https://api.mstock.trade/x?auth=token key:{FakeJwt}"),
            ("some.attribute", $"Bearer {FakeJwt}"));

        foreach (var tag in activity.TagObjects)
        {
            var value = tag.Value?.ToString() ?? string.Empty;
            Assert.DoesNotContain(FakeJwt, value);
            Assert.DoesNotContain("token key:", value);
        }
    }

    [Fact]
    public void No_span_attribute_contains_a_credential_after_processing()
    {
        var activity = ProcessedActivity(
            ("http.request.header.authorization", $"token my_api_key:{FakeJwt}"),
            ("http.request.method", "POST"),
            ("url.path", "/openapi/typea/orders"));

        foreach (var tag in activity.TagObjects)
        {
            var value = tag.Value?.ToString() ?? string.Empty;
            Assert.DoesNotContain("my_api_key", value);
            Assert.DoesNotContain(FakeJwt, value);
        }

        // Non-sensitive attributes survive.
        Assert.Equal("POST", activity.GetTagItem("http.request.method"));
        Assert.Equal("/openapi/typea/orders", activity.GetTagItem("url.path"));
    }
}
