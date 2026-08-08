// -------------------------------------------------------------------------------------
// <copyright file="ReaperDbTests.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Tests;

using Reaper.Db;

[TestFixture]
public class ReaperDbTests
{
   [SetUp]
   public void Setup()
   {
      this.db = new ReaperDb(":memory:");
   }

   [TearDown]
   public void TearDown()
   {
      this.db.Dispose();
   }

   private ReaperDb db = null!;

   [Test]
   public void Delete_nonexistent_path_is_noop()
   {
      Assert.DoesNotThrow(() => this.db.Delete(["does/not/exist.txt"]));
   }

   [Test]
   public void Delete_removes_entry()
   {
      this.db.Upsert([new Entry("foo/bar.txt", 1000L, 2000L, 500L)]);
      this.db.Delete(["foo/bar.txt"]);
      Assert.That(this.db.GetAll(), Is.Empty);
   }

   [Test]
   public void Delete_removes_multiple_entries_and_leaves_remainder()
   {
      this.db.Upsert(
         [
            new Entry("a.txt", 1000L, 2000L, 10L),
            new Entry("b.txt", 1001L, 2001L, 20L),
            new Entry("c.txt", 1002L, 2002L, 30L),
         ]);
      this.db.Delete(["a.txt", "c.txt"]);
      var remaining = this.db.GetAll();
      Assert.That(remaining, Has.Count.EqualTo(1));
      Assert.That(remaining[0].Path, Is.EqualTo("b.txt"));
   }

   [Test]
   public void GetAll_on_empty_db_returns_empty()
   {
      Assert.That(this.db.GetAll(), Is.Empty);
   }

   [Test]
   public void Touch_nonexistent_path_is_noop()
   {
      Assert.DoesNotThrow(() => this.db.Touch("nonexistent.txt", 5000L));
   }

   [Test]
   public void Touch_resets_refreshed_at_but_not_first_seen()
   {
      this.db.Upsert([new Entry("a.txt", 1000L, 2000L, 500L)]);
      this.db.Touch("a.txt", 5000L);
      var entry = this.db.GetAll().Single();
      Assert.That(entry.FirstSeen, Is.EqualTo(1000L));
      Assert.That(entry.RefreshedAt, Is.EqualTo(5000L));
   }

   [Test]
   public void Touch_returns_count_of_affected_rows()
   {
      this.db.Upsert(
         [
            new Entry("Foo/a.txt", 1000L, 2000L, 10L),
            new Entry("Foo/b.txt", 1001L, 2001L, 20L),
         ]);
      var count = this.db.Touch("Foo", 5000L);
      Assert.That(count, Is.EqualTo(2));
   }

   [Test]
   public void Touch_updates_all_entries_under_directory()
   {
      this.db.Upsert(
         [
            new Entry("Foo/a.txt", 1000L, 2000L, 10L),
            new Entry("Foo/b.txt", 1001L, 2001L, 20L),
            new Entry("Bar/c.txt", 1002L, 2002L, 30L),
         ]);
      this.db.Touch("Foo", 5000L);
      var all = this.db.GetAll().OrderBy(e => e.Path).ToList();
      Assert.That(all[0].RefreshedAt, Is.EqualTo(2002L)); // Bar/c.txt — unchanged
      Assert.That(all[1].RefreshedAt, Is.EqualTo(5000L)); // Foo/a.txt
      Assert.That(all[2].RefreshedAt, Is.EqualTo(5000L)); // Foo/b.txt
   }

   [Test]
   public void Upsert_inserts_multiple_entries()
   {
      this.db.Upsert(
         [
            new Entry("a.txt", 1000L, 2000L, 10L),
            new Entry("b.txt", 1001L, 2001L, 20L),
            new Entry("c.txt", 1002L, 2002L, 30L),
         ]);
      Assert.That(this.db.GetAll(), Has.Count.EqualTo(3));
   }

   [Test]
   public void Upsert_inserts_new_entry()
   {
      var entry = new Entry("foo/bar.txt", 1000L, 2000L, 500L);
      this.db.Upsert([entry]);
      var all = this.db.GetAll();
      Assert.That(all, Has.Count.EqualTo(1));
      Assert.That(all[0], Is.EqualTo(entry));
   }

   [Test]
   public void Upsert_never_overwrites_first_seen_on_conflict()
   {
      this.db.Upsert([new Entry("foo/bar.txt", 1000L, 2000L, 500L)]);
      this.db.Upsert([new Entry("foo/bar.txt", 3000L, 4000L, 500L)]);
      var entry = this.db.GetAll().Single();
      Assert.That(entry.FirstSeen, Is.EqualTo(1000L));
   }

   [Test]
   public void Upsert_updates_refreshed_at_and_size_for_existing_entry()
   {
      this.db.Upsert([new Entry("foo/bar.txt", 1000L, 2000L, 500L)]);
      this.db.Upsert([new Entry("foo/bar.txt", 1000L, 4000L, 900L)]);
      var all = this.db.GetAll();
      Assert.That(all, Has.Count.EqualTo(1));
      Assert.That(all[0], Is.EqualTo(new Entry("foo/bar.txt", 1000L, 4000L, 900L)));
   }
}
