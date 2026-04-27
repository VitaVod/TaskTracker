using System.Text.Json;
using System.Text.RegularExpressions;
using TimeZoneConverter;

namespace TaskTracker.Api.Features.Account.Validation;

public interface IAccountUpdateValidator
{
    AccountProfilePatchValidationResult ValidateProfilePatch(JsonElement payload);

    AccountSettingsPatchValidationResult ValidateSettingsPatch(JsonElement payload);
}

public sealed record AccountProfilePatchValidationResult(
    bool IsValid,
    bool HasDisplayName,
    string DisplayName,
    Dictionary<string, string[]> Errors);

public sealed record AccountSettingsPatchValidationResult(
    bool IsValid,
    bool HasTimeZoneId,
    string TimeZoneId,
    bool HasLocale,
    string Locale,
    Dictionary<string, string[]> Errors);

public class AccountUpdateValidator : IAccountUpdateValidator
{
    private static readonly Regex LocaleRegex = new("^[a-z]{2}(?:-[A-Z]{2})?$", RegexOptions.Compiled);

    public AccountProfilePatchValidationResult ValidateProfilePatch(JsonElement payload)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (payload.ValueKind != JsonValueKind.Object)
        {
            errors["$"] = ["Request body must be a JSON object."];
            return new AccountProfilePatchValidationResult(false, false, string.Empty, errors);
        }

        var hasDisplayName = false;
        var displayName = string.Empty;

        foreach (var property in payload.EnumerateObject())
        {
            if (!string.Equals(property.Name, "displayName", StringComparison.Ordinal))
            {
                errors[property.Name] = ["This field cannot be updated."];
                continue;
            }

            hasDisplayName = true;

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                errors["displayName"] = ["Display name must be a string value."];
                continue;
            }

            displayName = property.Value.GetString()?.Trim() ?? string.Empty;
            if (displayName.Length is < 2 or > 80)
            {
                errors["displayName"] = ["Display name must be between 2 and 80 characters."];
            }
        }

        if (!hasDisplayName)
        {
            errors["displayName"] = ["Display name is required."];
        }

        return new AccountProfilePatchValidationResult(errors.Count == 0, hasDisplayName, displayName, errors);
    }

    public AccountSettingsPatchValidationResult ValidateSettingsPatch(JsonElement payload)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (payload.ValueKind != JsonValueKind.Object)
        {
            errors["$"] = ["Request body must be a JSON object."];
            return new AccountSettingsPatchValidationResult(false, false, string.Empty, false, string.Empty, errors);
        }

        var hasTimeZoneId = false;
        var hasLocale = false;
        var timeZoneId = string.Empty;
        var locale = string.Empty;

        foreach (var property in payload.EnumerateObject())
        {
            switch (property.Name)
            {
                case "timeZoneId":
                    hasTimeZoneId = true;
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        errors["timeZoneId"] = ["Time zone must be a string value."];
                        break;
                    }

                    timeZoneId = property.Value.GetString()?.Trim() ?? string.Empty;
                    if (!IsValidTimeZone(timeZoneId))
                    {
                        errors["timeZoneId"] = ["The selected timezone is not valid."];
                    }

                    break;

                case "locale":
                    hasLocale = true;
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        errors["locale"] = ["Locale must be a string value."];
                        break;
                    }

                    locale = property.Value.GetString()?.Trim() ?? string.Empty;
                    if (!LocaleRegex.IsMatch(locale))
                    {
                        errors["locale"] = ["Locale must match the format ll or ll-RR (for example en or en-US)."];
                    }

                    break;

                default:
                    errors[property.Name] = ["This field cannot be updated."];
                    break;
            }
        }

        if (!hasTimeZoneId && !hasLocale)
        {
            errors["$"] = ["At least one settings field must be provided."];
        }

        return new AccountSettingsPatchValidationResult(
            errors.Count == 0,
            hasTimeZoneId,
            timeZoneId,
            hasLocale,
            locale,
            errors);
    }

    private static bool IsValidTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Length > 64)
        {
            return false;
        }

        try
        {
            _ = TZConvert.GetTimeZoneInfo(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
