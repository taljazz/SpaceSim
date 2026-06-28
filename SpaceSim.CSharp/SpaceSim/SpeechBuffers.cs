using System.Collections.Generic;

namespace SpaceSim;

/// <summary>
/// The channel a spoken message belongs to, so the player can focus the screen reader on one kind of
/// message at a time. <see cref="General"/> is the untagged default and is always audible (status, warnings,
/// menus); the rest are topic streams the player can filter to.
/// </summary>
public enum SpeechChannel { General, Navigation, Atlantean, System }

/// <summary>
/// A small set of message "buffers" (channels) the player can cycle through, reorder, and browse, much like
/// the channel list in an accessible chat client. The All view hears everything as it happens; focusing a
/// specific channel filters the live topic streams down to just that one and lets the player arrow back
/// through its recorded history. Pure logic with no audio or screen-reader dependency, so it is unit-tested
/// directly and the <see cref="Ship"/> simply asks it what to speak.
/// </summary>
public sealed class SpeechBuffers
{
    #region Views

    // The reorderable list of buffer "views" the player cycles through. A null entry is the "All" view
    // (everything is audible); the rest filter to a single channel. The order can be rearranged with the
    // reorder keys so the player's most-used buffers sit where they want them.
    private readonly List<SpeechChannel?> _order = new()
        { null, SpeechChannel.Navigation, SpeechChannel.Atlantean, SpeechChannel.System };

    private static string NameOf(SpeechChannel? view) => view?.ToString() ?? "All";

    #endregion

    #region State

    private const int MaxHistory = 200;

    private readonly List<(SpeechChannel Channel, string Message)> _history = new();
    private int _viewIndex;      // index into _order
    private int _browseCursor;   // index into _history; == _history.Count means "at the live end"

    /// <summary>The name of the buffer currently in focus (e.g. "All", "Navigation").</summary>
    public string ActiveViewName => NameOf(_order[_viewIndex]);

    #endregion

    #region Recording & filtering

    /// <summary>
    /// Records a message and reports whether it should be spoken aloud right now, given the active view.
    /// A new message always snaps the browse cursor back to the live end.
    /// </summary>
    public bool Record(SpeechChannel channel, string message)
    {
        _history.Add((channel, message));
        if (_history.Count > MaxHistory) _history.RemoveAt(0);
        _browseCursor = _history.Count;
        return ShouldSpeakLive(channel);
    }

    /// <summary>
    /// True when a message of this channel is audible under the active view. <see cref="SpeechChannel.General"/>
    /// (untagged status, warnings, menu read-outs, and other essentials) is always audible so focusing a
    /// buffer never hides safety-critical feedback; the topic channels are heard only in All or when focused.
    /// </summary>
    public bool ShouldSpeakLive(SpeechChannel channel)
    {
        if (channel == SpeechChannel.General) return true;
        SpeechChannel? filter = _order[_viewIndex];
        return filter == null || filter == channel;
    }

    #endregion

    #region Cycling, reordering & browsing

    /// <summary>Moves focus to the next (dir &gt; 0) or previous (dir &lt; 0) buffer, wrapping, and returns its name.</summary>
    public string CycleView(int dir)
    {
        int n = _order.Count;
        _viewIndex = ((_viewIndex + dir) % n + n) % n;
        _browseCursor = _history.Count;
        return ActiveViewName;
    }

    /// <summary>
    /// Moves the focused buffer one place earlier (dir &lt; 0) or later (dir &gt; 0) in the list, so the player
    /// can arrange the buffers to taste. Focus follows the moved buffer. Returns a phrase to announce; the ends
    /// do not wrap (so the player can feel where the edges are).
    /// </summary>
    public string MoveActiveView(int dir)
    {
        int target = _viewIndex + dir;
        if (target < 0 || target >= _order.Count)
            return $"{ActiveViewName} is already {(dir < 0 ? "first" : "last")}.";

        (_order[_viewIndex], _order[target]) = (_order[target], _order[_viewIndex]);
        _viewIndex = target;
        return $"Moved {ActiveViewName} to position {_viewIndex + 1} of {_order.Count}.";
    }

    /// <summary>
    /// Steps the browse cursor through the active view's history. dir = -1 reads the previous (older) message,
    /// +1 the next (newer). Returns the message, or null when there is none in that direction.
    /// </summary>
    public string? Browse(int dir)
    {
        SpeechChannel? filter = _order[_viewIndex];
        int i = _browseCursor;
        while (true)
        {
            i += dir;
            if (i < 0 || i >= _history.Count) return null;          // ran off an edge of the buffer
            if (filter == null || _history[i].Channel == filter)    // entry belongs to the active view
            {
                _browseCursor = i;
                return _history[i].Message;
            }
        }
    }

    #endregion
}
