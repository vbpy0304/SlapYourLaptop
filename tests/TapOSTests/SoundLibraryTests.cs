using TapOS.Services;
using Xunit;

namespace TapOSTests;

public sealed class SoundLibraryTests
{
    [Fact]
    public void MissingFolderReturnsZeroAndDiagnostic()
    {
        var library = new SoundLibrary();
        var count = library.Reload(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Equal(0, count);
        Assert.NotNull(library.LastError);
    }

    [Fact]
    public void EmptyFolderCannotPlay()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            var library = new SoundLibrary();
            library.Reload(folder.FullName);

            Assert.False(library.TryPlayRandom(out _, out var error));
            Assert.NotNull(error);
        }
        finally
        {
            Directory.Delete(folder.FullName, true);
        }
    }

    [Fact]
    public void FourFingerReleaseWithinLimitTriggersOnce()
    {
        var recognizer = new FourFingerGestureRecognizer(TimeSpan.FromMilliseconds(500));
        var start = DateTimeOffset.UtcNow;

        Assert.False(recognizer.Process(4, start));
        Assert.False(recognizer.Process(4, start.AddMilliseconds(200)));
        Assert.True(recognizer.Process(0, start.AddMilliseconds(499)));
        Assert.False(recognizer.Process(0, start.AddMilliseconds(500)));
    }

    [Fact]
    public void FourFingerReleaseAfterLimitDoesNotTrigger()
    {
        var recognizer = new FourFingerGestureRecognizer(TimeSpan.FromMilliseconds(500));
        var start = DateTimeOffset.UtcNow;

        Assert.False(recognizer.Process(4, start));
        Assert.False(recognizer.Process(0, start.AddMilliseconds(501)));
    }

    [Fact]
    public void MoreThanFourContactsDoesNotTriggerFourFingerGesture()
    {
        var recognizer = new FourFingerGestureRecognizer(TimeSpan.FromMilliseconds(500));
        var start = DateTimeOffset.UtcNow;

        Assert.False(recognizer.Process(4, start));
        Assert.False(recognizer.Process(5, start.AddMilliseconds(50)));
        Assert.False(recognizer.Process(0, start.AddMilliseconds(100)));
    }
}
