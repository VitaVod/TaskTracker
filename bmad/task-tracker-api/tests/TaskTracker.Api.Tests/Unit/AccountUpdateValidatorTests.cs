using System.Text.Json;
using TaskTracker.Api.Features.Account.Validation;

namespace TaskTracker.Api.Tests.Unit;

public class AccountUpdateValidatorTests
{
    private readonly AccountUpdateValidator _validator = new();

    [Fact]
    public void ValidateProfilePatch_WithAllowedField_Succeeds()
    {
        var payload = Parse("""{ "displayName": "Casey Sprint" }""");

        var result = _validator.ValidateProfilePatch(payload);

        Assert.True(result.IsValid);
        Assert.True(result.HasDisplayName);
        Assert.Equal("Casey Sprint", result.DisplayName);
    }

    [Fact]
    public void ValidateProfilePatch_WithUnknownField_Fails()
    {
        var payload = Parse("""{ "email": "not.allowed@example.com" }""");

        var result = _validator.ValidateProfilePatch(payload);

        Assert.False(result.IsValid);
        Assert.Contains("email", result.Errors.Keys);
    }

    [Fact]
    public void ValidateSettingsPatch_WithInvalidTimeZone_Fails()
    {
        var payload = Parse("""{ "timeZoneId": "Moon/Base" }""");

        var result = _validator.ValidateSettingsPatch(payload);

        Assert.False(result.IsValid);
        Assert.Contains("timeZoneId", result.Errors.Keys);
    }

    [Fact]
    public void ValidateSettingsPatch_WithValidValues_Succeeds()
    {
        var payload = Parse("""{ "timeZoneId": "UTC", "locale": "en-US" }""");

        var result = _validator.ValidateSettingsPatch(payload);

        Assert.True(result.IsValid);
        Assert.True(result.HasTimeZoneId);
        Assert.True(result.HasLocale);
        Assert.Equal("UTC", result.TimeZoneId);
        Assert.Equal("en-US", result.Locale);
    }

    [Fact]
    public void ValidateSettingsPatch_WithIanaTimeZone_Succeeds()
    {
        var payload = Parse("""{ "timeZoneId": "Europe/Kyiv", "locale": "uk-UA" }""");

        var result = _validator.ValidateSettingsPatch(payload);

        Assert.True(result.IsValid);
        Assert.Equal("Europe/Kyiv", result.TimeZoneId);
        Assert.Equal("uk-UA", result.Locale);
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
