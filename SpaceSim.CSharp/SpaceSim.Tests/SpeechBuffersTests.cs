using SpaceSim;
using Xunit;

namespace SpaceSim.Tests;

/// <summary>
/// Tests for <see cref="SpeechBuffers"/> — the channel filtering and history browsing behind the in-game
/// message buffers ('[' and ']' cycle the focused buffer, ',' and '.' step through its history).
/// </summary>
public class SpeechBuffersTests
{
    #region Live filtering

    [Fact]
    public void AllView_IsTheDefault_AndHearsEveryChannel()
    {
        var b = new SpeechBuffers();
        Assert.Equal("All", b.ActiveViewName);
        Assert.True(b.ShouldSpeakLive(SpeechChannel.Navigation));
        Assert.True(b.ShouldSpeakLive(SpeechChannel.Atlantean));
        Assert.True(b.ShouldSpeakLive(SpeechChannel.General));
    }

    [Fact]
    public void FocusedView_SpeaksOwnChannelAndGeneral_MutesOtherTopics()
    {
        var b = new SpeechBuffers();
        b.CycleView(+1); // All -> Navigation
        Assert.Equal("Navigation", b.ActiveViewName);
        Assert.True(b.ShouldSpeakLive(SpeechChannel.Navigation));
        Assert.True(b.ShouldSpeakLive(SpeechChannel.General)); // status/warnings stay audible everywhere
        Assert.False(b.ShouldSpeakLive(SpeechChannel.Atlantean));
        Assert.False(b.ShouldSpeakLive(SpeechChannel.System));
    }

    [Fact]
    public void Record_ReportsWhetherTheMessageIsAudibleNow()
    {
        var b = new SpeechBuffers();
        Assert.True(b.Record(SpeechChannel.Atlantean, "heard in All"));
        b.CycleView(+1); // Navigation
        Assert.False(b.Record(SpeechChannel.Atlantean, "filtered out"));
        Assert.True(b.Record(SpeechChannel.Navigation, "heard in Navigation"));
    }

    #endregion

    #region Cycling

    [Fact]
    public void CycleView_WrapsInBothDirections()
    {
        var b = new SpeechBuffers();
        Assert.Equal("System", b.CycleView(-1)); // wrap backwards from All to the last view
        Assert.Equal("All", b.CycleView(+1));    // forward, wrapping back to All
    }

    [Fact]
    public void MoveActiveView_ReordersAndFocusFollows()
    {
        var b = new SpeechBuffers();
        b.CycleView(+1); // focus Navigation (index 1)
        Assert.Equal("Navigation", b.ActiveViewName);

        b.MoveActiveView(-1);                          // swap it earlier, ahead of All
        Assert.Equal("Navigation", b.ActiveViewName);  // focus follows the moved buffer
        Assert.Equal("All", b.CycleView(+1));          // All now sits one place later
    }

    [Fact]
    public void MoveActiveView_DoesNotWrapPastTheEnds()
    {
        var b = new SpeechBuffers(); // focused on All at the first position
        Assert.Contains("first", b.MoveActiveView(-1));
        Assert.Equal("All", b.ActiveViewName);
    }

    #endregion

    #region Browsing

    [Fact]
    public void Browse_StepsBackThroughHistoryNewestFirst()
    {
        var b = new SpeechBuffers();
        b.Record(SpeechChannel.General, "one");
        b.Record(SpeechChannel.General, "two");
        b.Record(SpeechChannel.General, "three");

        Assert.Equal("three", b.Browse(-1));
        Assert.Equal("two", b.Browse(-1));
        Assert.Equal("one", b.Browse(-1));
        Assert.Null(b.Browse(-1)); // ran off the start
    }

    [Fact]
    public void Browse_CanStepForwardAgain()
    {
        var b = new SpeechBuffers();
        b.Record(SpeechChannel.General, "one");
        b.Record(SpeechChannel.General, "two");

        Assert.Equal("two", b.Browse(-1));
        Assert.Equal("one", b.Browse(-1));
        Assert.Equal("two", b.Browse(+1));
        Assert.Null(b.Browse(+1)); // back at the live end
    }

    [Fact]
    public void Browse_OnlyVisitsTheActiveChannel()
    {
        var b = new SpeechBuffers();
        b.Record(SpeechChannel.Navigation, "nav1");
        b.Record(SpeechChannel.Atlantean, "atl1");
        b.Record(SpeechChannel.Navigation, "nav2");

        b.CycleView(+1); // Navigation
        Assert.Equal("nav2", b.Browse(-1));
        Assert.Equal("nav1", b.Browse(-1)); // skips the Atlantean entry between them
        Assert.Null(b.Browse(-1));
    }

    [Fact]
    public void Browse_EmptyHistory_ReturnsNull()
    {
        var b = new SpeechBuffers();
        Assert.Null(b.Browse(-1));
        Assert.Null(b.Browse(+1));
    }

    [Fact]
    public void NewMessage_ResetsBrowseCursorToLiveEnd()
    {
        var b = new SpeechBuffers();
        b.Record(SpeechChannel.General, "one");
        b.Record(SpeechChannel.General, "two");
        Assert.Equal("two", b.Browse(-1)); // cursor now sits on "two"

        b.Record(SpeechChannel.General, "three"); // a new message should snap browsing back to the end
        Assert.Equal("three", b.Browse(-1));
    }

    #endregion
}
