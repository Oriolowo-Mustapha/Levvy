using Levvy.Api.Hubs;
using Levvy.Domain.Entities;

namespace Levvy.Tests;

/// <summary>
/// Caption sentence-merging: the 5s ASR chunks arrive with fragments that cut
/// mid-sentence; the room must hold open tails and join them with the next
/// chunk so the transcript reads in sentences instead of 5s slices.
/// </summary>
public class CaptionMergeTests
{
    private static TranscriptSegment Seg(long offsetMs, string text) => new()
    {
        Id = Guid.NewGuid(),
        OffsetMs = offsetMs,
        Text = text,
        SourceLanguage = "en",
        CreatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public void Open_tail_is_held_and_joined_with_next_chunk_at_the_tail_offset()
    {
        var room = new SessionRoom(Guid.NewGuid(), "KDL-A6T");

        var first = room.PrepareForBroadcast(new[] { Seg(0, "Cross-price elasticity measures how quantity demanded of good A") });
        Assert.Empty(first); // sentence still open, nothing broadcast yet

        var second = room.PrepareForBroadcast(new[] { Seg(5000, "responds to a price change in good B.") });

        var segment = Assert.Single(second);
        Assert.Equal(0, segment.OffsetMs); // receipt anchored to where the sentence began
        Assert.Equal("Cross-price elasticity measures how quantity demanded of good A responds to a price change in good B.", segment.Text);
    }

    [Fact]
    public void Complete_sentences_broadcast_immediately_and_new_tail_is_held()
    {
        var room = new SessionRoom(Guid.NewGuid(), "KDL-A6T");

        var ready = room.PrepareForBroadcast(new[]
        {
            Seg(0, "Demand for the original product shifts left."),
            Seg(3000, "That is the whole mechanism"),
        });

        var segment = Assert.Single(ready);
        Assert.Equal(0, segment.OffsetMs);
        Assert.Equal(room.TakePendingTail()!.Text, "That is the whole mechanism");
    }

    [Fact]
    public void Speaker_change_flushes_the_tail_instead_of_merging()
    {
        var room = new SessionRoom(Guid.NewGuid(), "KDL-A6T");

        room.PrepareForBroadcast(new[] { Seg(0, "[Speaker 1] The subsidy shifts supply down") });

        var ready = room.PrepareForBroadcast(new[] { Seg(5000, "[Speaker 2] And consumers pay less at the register.") });

        Assert.Equal(2, ready.Count);
        Assert.Equal(0, ready[0].OffsetMs);
        Assert.EndsWith("supply down", ready[0].Text, StringComparison.Ordinal);
        Assert.Equal(5000, ready[1].OffsetMs);
    }

    [Fact]
    public void Silence_chunk_flushes_a_held_tail()
    {
        var room = new SessionRoom(Guid.NewGuid(), "KDL-A6T");

        room.PrepareForBroadcast(new[] { Seg(0, "The lecturer pauses right after saying") });

        var flushed = room.PrepareForBroadcast(Array.Empty<TranscriptSegment>());

        var segment = Assert.Single(flushed);
        Assert.Equal(0, segment.OffsetMs);
    }

    [Fact]
    public void Session_end_takes_the_pending_tail()
    {
        var room = new SessionRoom(Guid.NewGuid(), "KDL-A6T");

        room.PrepareForBroadcast(new[] { Seg(0, "Final thought before we wrap up") });

        var tail = room.TakePendingTail();
        Assert.NotNull(tail);
        Assert.Null(room.TakePendingTail()); // second take is empty
    }

    [Fact]
    public void Runaway_tail_is_flushed_instead_of_held_forever()
    {
        var room = new SessionRoom(Guid.NewGuid(), "KDL-A6T");
        var longText = new string('x', 320); // no terminal punctuation, over the cap

        var ready = room.PrepareForBroadcast(new[] { Seg(0, longText) });

        Assert.Single(ready);
        Assert.Null(room.TakePendingTail());
    }
}
