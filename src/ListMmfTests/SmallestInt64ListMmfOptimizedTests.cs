using System.IO;
using BruSoftware.ListMmf;
using Xunit;

namespace ListMmfTests;

public class SmallestInt64ListMmfOptimizedTests
{
    private readonly string _testPath = Path.GetTempFileName();

    private void Dispose()
    {
        try { File.Delete(_testPath); }
        catch { }
        try { File.Delete(_testPath + ".upgrading"); }
        catch { }
        try { File.Delete(_testPath + ".backup"); }
        catch { }
    }

    [Fact]
    public void CleanupUpgradeFiles_RemovesLeftoverFiles()
    {
        // Arrange - create leftover upgrade files
        File.WriteAllText(_testPath + ".upgrading", "test");
        File.WriteAllText(_testPath + ".backup", "test");

        // Act
        SmallestInt64ListMmfOptimized.CleanupUpgradeFiles(_testPath);

        // Assert
        Assert.False(File.Exists(_testPath + ".upgrading"));
        Assert.False(File.Exists(_testPath + ".backup"));
    }

    [Fact]
    public void CleanupUpgradeFiles_RestoresFromBackupWhenOriginalMissing()
    {
        // Arrange - backup exists but original doesn't
        File.Delete(_testPath);
        File.WriteAllText(_testPath + ".backup", "restored content");

        // Act
        SmallestInt64ListMmfOptimized.CleanupUpgradeFiles(_testPath);

        // Assert
        Assert.True(File.Exists(_testPath));
        Assert.False(File.Exists(_testPath + ".backup"));
        Assert.Equal("restored content", File.ReadAllText(_testPath));
    }
}