using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Levvy.Infrastructure.Llm;

namespace Levvy.Tests;

/// <summary>
/// Study-pack topic parts ("chapters") are navigational, but grounded the same
/// way as flashcards: each part must start at a real transcript offset.
/// </summary>
public class StudyPackPartsTests
{
    private static TranscriptSegment Seg(long offsetMs, string text) => new()
    {
        Id = Guid.NewGuid(),
        OffsetMs = offsetMs,
        Text = text,
        SourceLanguage = "en",
        CreatedAtUtc = DateTime.UtcNow
    };

    private static string PackJson(string partsJson) => $$"""
        {
          "summary": "Intro to markets [00:00]",
          "flashcards": [],
          "parts": {{partsJson}}
        }
        """;

    [Fact]
    public void Parts_snap_to_real_offsets_and_are_ordered_deduped()
    {
        var transcript = new[]
        {
            Seg(0, "Welcome to the course."),
            Seg(30000, "Supply and demand basics."),
            Seg(120000, "Elasticity of demand."),
        };

        var result = GroundingVerifier.ParseAndValidateStudyPack(
            PackJson("""[{"title":"Opening","startOffsetMs":0},{"title":"Elasticity","startOffsetMs":130000},{"title":"Elasticity","startOffsetMs":120000}]"""),
            transcript);

        var parts = result.Parts.ToList();
        Assert.Equal(2, parts.Count); // ungrounded 130000 dropped, duplicate 120000 deduped
        Assert.Equal(0, parts[0].StartOffsetMs);
        Assert.Equal(120000, parts[1].StartOffsetMs);
        Assert.Equal("Opening", parts[0].Title);
    }

    [Fact]
    public void Missing_or_malformed_parts_yield_empty_list_without_failing_the_pack()
    {
        var transcript = new[] { Seg(0, "Hello."), Seg(5000, "World.") };

        var noParts = GroundingVerifier.ParseAndValidateStudyPack(
            PackJson("[]"), transcript);
        var broken = GroundingVerifier.ParseAndValidateStudyPack("not json at all", transcript);

        Assert.Empty(noParts.Parts);
        Assert.NotEqual("Study pack unavailable.", noParts.Summary); // valid JSON, just no parts
        Assert.Empty(broken.Parts);
        Assert.Equal("Study pack unavailable.", broken.Summary); // wholly malformed body
    }
}
