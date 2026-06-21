using Preagonal.GServer.Persistence;
using Xunit;

namespace Preagonal.GServer.Persistence.Tests;

public sealed class Gs2SettingsTests
{
    [Fact]
    public void ParseMatchesCppCommentBlankKeyLowercaseAndTrimRules()
    {
        var settings = Gs2Settings.Parse(
            """
            # full-line comment
              Name = My Server # inline comment
            no-separator
            STAFF = Alice
            staff = Bob
            complex = one=two=three

            """);

        Assert.False(settings.Exists("no-separator"));
        Assert.True(settings.Exists("NAME"));
        Assert.Equal("My Server", settings.GetString("name"));
        Assert.Equal("Alice,Bob", settings.GetString("staff"));
        Assert.Equal("one=two=three", settings.GetString("complex"));
        Assert.Equal("fallback", settings.GetString("missing", "fallback"));
    }

    [Fact]
    public void FromRcDuplicateKeysReplaceInsteadOfAppend()
    {
        var settings = Gs2Settings.Parse(
            """
            staff = Alice
            staff = Bob
            """,
            fromRc: true);

        Assert.Equal("Bob", settings.GetString("staff"));
    }

    [Fact]
    public void TypedAccessorsUseCppDefaultsAndBoolTruthRules()
    {
        var settings = Gs2Settings.Parse(
            """
            enabled = true
            one = 1
            yes = True
            falseish = false
            count = 42abc
            ratio = 3.5suffix
            """);

        Assert.True(settings.GetBool("enabled", false));
        Assert.True(settings.GetBool("one", false));
        Assert.False(settings.GetBool("yes", false));
        Assert.False(settings.GetBool("falseish", true));
        Assert.True(settings.GetBool("missing"));
        Assert.Equal(42, settings.GetInt("count", 7));
        Assert.Equal(7, settings.GetInt("missing", 7));
        Assert.Equal(3.5f, settings.GetFloat("ratio", 1.0f));
        Assert.Equal(1.0f, settings.GetFloat("missing"));
    }

    [Fact]
    public void LoadFileReturnsUnopenedSettingsWhenFileIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config", "serveroptions.txt");

        var settings = Gs2Settings.LoadFile(path);

        Assert.False(settings.IsOpened);
        Assert.False(settings.Exists("name"));
    }

    [Fact]
    public void LoadFileReadsExistingFile()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "serveroptions.txt");
        File.WriteAllText(path, "name = Test Server\r\n");

        var settings = Gs2Settings.LoadFile(path);

        Assert.True(settings.IsOpened);
        Assert.Equal("Test Server", settings.GetString("name"));
    }
}
