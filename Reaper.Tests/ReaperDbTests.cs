using Reaper.Db;

namespace Reaper.Tests;

[TestFixture]
public class ReaperDbTests
{
    private ReaperDb _db = null!;

    [SetUp]
    public void Setup() => _db = new ReaperDb(":memory:");

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public void GetAll_on_empty_db_returns_empty()
    {
        Assert.That(_db.GetAll(), Is.Empty);
    }

    [Test]
    public void Upsert_inserts_new_entry()
    {
        var entry = new Entry("foo/bar.txt", 1000L, 2000L);
        _db.Upsert([entry]);
        var all = _db.GetAll();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0], Is.EqualTo(entry));
    }

    [Test]
    public void Upsert_updates_existing_entry()
    {
        _db.Upsert([new Entry("foo/bar.txt", 1000L, 2000L)]);
        _db.Upsert([new Entry("foo/bar.txt", 3000L, 4000L)]);
        var all = _db.GetAll();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0], Is.EqualTo(new Entry("foo/bar.txt", 3000L, 4000L)));
    }

    [Test]
    public void Upsert_inserts_multiple_entries()
    {
        _db.Upsert([
            new Entry("a.txt", 1000L, 2000L),
            new Entry("b.txt", 1001L, 2001L),
            new Entry("c.txt", 1002L, 2002L),
        ]);
        Assert.That(_db.GetAll(), Has.Count.EqualTo(3));
    }

    [Test]
    public void Delete_removes_entry()
    {
        _db.Upsert([new Entry("foo/bar.txt", 1000L, 2000L)]);
        _db.Delete(["foo/bar.txt"]);
        Assert.That(_db.GetAll(), Is.Empty);
    }

    [Test]
    public void Delete_nonexistent_path_is_noop()
    {
        Assert.DoesNotThrow(() => _db.Delete(["does/not/exist.txt"]));
    }

    [Test]
    public void Touch_updates_first_seen_for_exact_path()
    {
        _db.Upsert([new Entry("a.txt", 1000L, 2000L)]);
        _db.Touch("a.txt", 5000L);
        var entry = _db.GetAll().Single();
        Assert.That(entry.FirstSeen, Is.EqualTo(5000L));
        Assert.That(entry.UpdatedAt, Is.EqualTo(5000L));
    }

    [Test]
    public void Touch_updates_all_entries_under_directory()
    {
        _db.Upsert([
            new Entry("Foo/a.txt", 1000L, 2000L),
            new Entry("Foo/b.txt", 1001L, 2001L),
            new Entry("Bar/c.txt", 1002L, 2002L),
        ]);
        _db.Touch("Foo", 5000L);
        var all = _db.GetAll().OrderBy(e => e.Path).ToList();
        Assert.That(all[0].FirstSeen, Is.EqualTo(1002L));   // Bar/c.txt — unchanged
        Assert.That(all[1].FirstSeen, Is.EqualTo(5000L));   // Foo/a.txt
        Assert.That(all[2].FirstSeen, Is.EqualTo(5000L));   // Foo/b.txt
    }

    [Test]
    public void Touch_returns_count_of_affected_rows()
    {
        _db.Upsert([
            new Entry("Foo/a.txt", 1000L, 2000L),
            new Entry("Foo/b.txt", 1001L, 2001L),
        ]);
        var count = _db.Touch("Foo", 5000L);
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void Touch_nonexistent_path_is_noop()
    {
        Assert.DoesNotThrow(() => _db.Touch("nonexistent.txt", 5000L));
    }

    [Test]
    public void Delete_removes_multiple_entries_and_leaves_remainder()
    {
        _db.Upsert([
            new Entry("a.txt", 1000L, 2000L),
            new Entry("b.txt", 1001L, 2001L),
            new Entry("c.txt", 1002L, 2002L),
        ]);
        _db.Delete(["a.txt", "c.txt"]);
        var remaining = _db.GetAll();
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].Path, Is.EqualTo("b.txt"));
    }
}
