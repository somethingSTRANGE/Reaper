using Reaper;
using Reaper.Pruning;

namespace Reaper.Tests;

[TestFixture]
public class PrunerTests
{
    private const long Now = 1_000_000_000L;
    private const long Day = 86_400L;
    private const long SevenDays = 7 * Day;

    private static Entry Aged(string path, int days) =>
        new(path, Now - days * Day, Now);

    [Test]
    public void Fresh_entry_is_not_flagged()
    {
        var flagged = Pruner.FlagForRemoval([Aged("file.txt", 0)], SevenDays, Now);
        Assert.That(flagged, Is.Empty);
    }

    [Test]
    public void Entry_exactly_at_threshold_is_not_flagged()
    {
        var flagged = Pruner.FlagForRemoval([Aged("file.txt", 7)], SevenDays, Now);
        Assert.That(flagged, Is.Empty);
    }

    [Test]
    public void Entry_past_threshold_is_flagged()
    {
        var flagged = Pruner.FlagForRemoval([Aged("file.txt", 8)], SevenDays, Now);
        Assert.That(flagged, Contains.Item("file.txt"));
    }

    [Test]
    public void Retained_file_protects_stale_sibling()
    {
        var entries = new[]
        {
            Aged("Foo/young.txt", 2),
            Aged("Foo/old.txt", 30),
        };
        var flagged = Pruner.FlagForRemoval(entries, SevenDays, Now);
        Assert.That(flagged, Is.Empty);
    }

    [Test]
    public void Retained_file_protects_entire_ancestor_chain_and_all_contents()
    {
        var entries = new[]
        {
            Aged("Foo/Bar/young.txt", 2),
            Aged("Foo/Bar/old.log", 30),
            Aged("Foo/other.txt", 30),
        };
        var flagged = Pruner.FlagForRemoval(entries, SevenDays, Now);
        Assert.That(flagged, Is.Empty);
    }

    [Test]
    public void Retained_file_near_root_protects_deeper_descendants()
    {
        var entries = new[]
        {
            Aged("Foo/Bar/young.txt", 30),
            Aged("Foo/Bar/old.log", 30),
            Aged("Foo/other.txt", 2),
        };
        var flagged = Pruner.FlagForRemoval(entries, SevenDays, Now);
        Assert.That(flagged, Is.Empty);
    }

    [Test]
    public void Fully_stale_subtree_is_flagged()
    {
        var entries = new[]
        {
            Aged("Stale/old1.txt", 30),
            Aged("Stale/old2.txt", 30),
        };
        var flagged = Pruner.FlagForRemoval(entries, SevenDays, Now);
        Assert.That(flagged, Is.EquivalentTo(new[] { "Stale/old1.txt", "Stale/old2.txt" }));
    }

    [Test]
    public void Retention_does_not_bleed_across_sibling_subtrees()
    {
        var entries = new[]
        {
            Aged("ProjectFoo/Bar/young.txt", 2),
            Aged("ProjectFoo/Bar/old.log", 30),
            Aged("ProjectFoo/other.txt", 30),
            Aged("StaleStuff/a.txt", 30),
        };
        var flagged = Pruner.FlagForRemoval(entries, SevenDays, Now);
        Assert.That(flagged, Is.EquivalentTo(new[] { "StaleStuff/a.txt" }));
    }

    [Test]
    public void Empty_entry_list_returns_empty()
    {
        var flagged = Pruner.FlagForRemoval([], SevenDays, Now);
        Assert.That(flagged, Is.Empty);
    }

    [Test]
    public void All_fresh_entries_return_empty()
    {
        var entries = new[] { Aged("a.txt", 1), Aged("b.txt", 3), Aged("c.txt", 7) };
        var flagged = Pruner.FlagForRemoval(entries, SevenDays, Now);
        Assert.That(flagged, Is.Empty);
    }

    [Test]
    public void Stale_directory_entry_is_protected_by_retained_child()
    {
        var entries = new[]
        {
            Aged("Foo", 30),
            Aged("Foo/Bar", 30),
            Aged("Foo/Bar/young.txt", 2),
        };
        var flagged = Pruner.FlagForRemoval(entries, SevenDays, Now);
        Assert.That(flagged, Is.Empty);
    }
}
