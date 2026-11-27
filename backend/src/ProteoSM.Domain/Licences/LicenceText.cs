namespace ProteoSM.Domain.Licences;

internal static class LicenceText
{
    public static string Required(string? value, int maxLength, string parameter)
    {
        var clean = value?.Trim() ?? string.Empty;
        return clean.Length > 0 && clean.Length <= maxLength
            ? clean
            : throw new ArgumentException($"A value of 1 to {maxLength} characters is needed.", parameter);
    }

    public static string? Optional(string? value, int maxLength, string parameter)
    {
        var clean = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return clean is null || clean.Length <= maxLength
            ? clean
            : throw new ArgumentException($"At most {maxLength} characters are allowed.", parameter);
    }
}
