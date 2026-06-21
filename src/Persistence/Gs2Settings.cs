using System.Globalization;

namespace Preagonal.GServer.Persistence;

public sealed class Gs2Settings : IAccountLoadSettings
{
    private readonly Dictionary<string, SettingValue> _keys = new(StringComparer.Ordinal);

    private Gs2Settings(bool isOpened)
    {
        IsOpened = isOpened;
    }

    public bool IsOpened { get; }

    public static Gs2Settings Parse(string settings, string separator = "=", bool fromRc = false)
    {
        var parsed = new Gs2Settings(isOpened: true);
        parsed.LoadSettings(settings, separator, fromRc);
        return parsed;
    }

    public static Gs2Settings LoadFile(string path, string separator = "=")
    {
        if (!File.Exists(path))
            return new Gs2Settings(isOpened: false);

        var parsed = new Gs2Settings(isOpened: true);
        parsed.LoadSettings(File.ReadAllText(path), separator, fromRc: false);
        return parsed;
    }

    public bool Exists(string key) =>
        _keys.ContainsKey(key.ToLowerInvariant());

    public bool GetBool(string key, bool defaultValue = true)
    {
        var value = GetValue(key);
        return value is null ? defaultValue : value == "true" || value == "1";
    }

    public float GetFloat(string key, float defaultValue = 1.0f)
    {
        var value = GetValue(key);
        return value is null ? defaultValue : Strtof(value);
    }

    public int GetInt(string key, int defaultValue = 1)
    {
        var value = GetValue(key);
        return value is null ? defaultValue : Atoi(value);
    }

    public string GetString(string key, string defaultValue = "")
    {
        var value = GetValue(key);
        return value ?? defaultValue;
    }

    private string? GetValue(string key) =>
        _keys.TryGetValue(key.ToLowerInvariant(), out var value) ? value.Value : null;

    private void LoadSettings(string settings, string separator, bool fromRc)
    {
        settings = settings.Replace("\r", string.Empty, StringComparison.Ordinal);
        var lines = settings.Split('\n').ToList();
        if (lines.Count > 0 && TrimCString(lines[^1]).Length == 0)
            lines.RemoveAt(lines.Count - 1);

        foreach (var originalLine in lines)
        {
            var line = originalLine;
            if (line.IndexOf('#', StringComparison.Ordinal) == 0)
                continue;
            if (line.Length == 0 || !line.Contains(separator, StringComparison.Ordinal))
                continue;

            var parts = line.Split(separator);
            parts[0] = parts[0].ToLowerInvariant();
            if (parts.Length == 1)
                continue;

            if (parts.Length > 2)
            {
                for (var i = 2; i < parts.Length; i++)
                    parts[1] += separator + parts[i];
            }

            var name = TrimCString(parts[0]);
            var rawValue = TrimCString(parts[1]);
            var incoming = SettingValue.FromRaw(rawValue);

            if (!_keys.TryGetValue(name, out var existing))
            {
                _keys.Add(name, incoming);
                continue;
            }

            _keys[name] = fromRc
                ? incoming
                : existing with { Value = existing.Value + "," + incoming.Value };
        }
    }

    private static string TrimCString(string value) =>
        value.Trim();

    private static int Atoi(string value)
    {
        var parsed = ParseIntegerPrefix(value);
        return unchecked((int)parsed);
    }

    private static long ParseIntegerPrefix(string value)
    {
        var index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
            index++;

        var sign = 1L;
        if (index < value.Length && (value[index] == '-' || value[index] == '+'))
        {
            sign = value[index] == '-' ? -1L : 1L;
            index++;
        }

        var result = 0L;
        while (index < value.Length && char.IsAsciiDigit(value[index]))
        {
            result = unchecked(result * 10 + (value[index] - '0'));
            index++;
        }

        return unchecked(result * sign);
    }

    private static float Strtof(string value)
    {
        var trimmed = value.TrimStart();
        var length = 0;
        while (length < trimmed.Length && IsFloatPrefixChar(trimmed[length], length))
            length++;

        if (length == 0)
            return 0;

        return float.TryParse(trimmed[..length], NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static bool IsFloatPrefixChar(char value, int index) =>
        char.IsAsciiDigit(value) || value == '.' || value == 'e' || value == 'E' ||
        ((value == '-' || value == '+') && index == 0);

    private sealed record SettingValue(string Value, string RawValue)
    {
        public static SettingValue FromRaw(string raw)
        {
            var commentPosition = raw.IndexOf('#', StringComparison.Ordinal);
            if (commentPosition == -1)
                return new SettingValue(raw, string.Empty);

            var value = TrimCString(raw[..commentPosition]);
            return new SettingValue(value, raw[value.Length..]);
        }
    }
}
