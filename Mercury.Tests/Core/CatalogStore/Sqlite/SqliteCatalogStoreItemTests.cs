using System;
using System.Collections;
using System.IO;

using NUnit.Framework;

using Mercury.Core.CatalogStore;
using Mercury.Core.CatalogStore.Sqlite;

namespace Mercury.Tests.Core.CatalogStore.Sqlite
{
	[TestFixture]
	public class SqliteCatalogStoreItemTests
	{
		//Keep two stores open with different connection objects, to ensure
		//two independent views of the database
		static SqliteCatalogStore _store = null, _store2 = null;

		[SetUp]
		public void CreateStore() {
			SqliteCatalogStoreManager mgr = new SqliteCatalogStoreManager();
			_store = mgr.CreateCatalogStore(Path.GetTempFileName(), true);
			_store2 = mgr.OpenCatalogStore(_store.Uri);
		}

		[TearDown]
		public void RemoveStore() {
			String storePath = _store.Uri;
			_store.Dispose();
			_store = null;
			_store2.Dispose();
			_store2 = null;
			File.Delete(storePath);
		}

		[Test]
		public void BasicElementsTest() {
			//Exercise the basic properties on a new item not yet persisted
			//to the database
			SqliteCatalogStoreItem item = _store.CreateItem();

			Assert.AreEqual(null, item.Parent);
			Assert.AreEqual(null, item.AliasOf);
			Assert.AreEqual(null, item.Title);
			Assert.AreEqual(null, item.Type);
			Assert.AreEqual(null, item.Uri);
			Assert.AreEqual(0, item.Tags.Count);
			Assert.AreEqual(0, item.Aliases.Count);
			Assert.AreEqual(0, item.Children.Count);

			item.Title = "Foo";
			item.Type = "fso";
			item.Uri = "foo/bar/baz";

			Assert.AreEqual("Foo", item.Title);
			Assert.AreEqual("fso", item.Type);
			Assert.AreEqual("foo/bar/baz", item.Uri);
			
			SqliteCatalogStoreItem itemParent = _store.CreateItem();
			SqliteCatalogStoreItem itemAlias = _store.CreateItem();

			item.Parent = itemParent;
			item.AliasOf = itemAlias;

			Assert.AreEqual(itemParent, item.Parent);
			Assert.AreEqual(itemAlias, item.AliasOf);
		}

		[Test]
		public void AliasListUpdatesTest() {
			//The Aliases list cannot be edited directly; items are
			//added in and out as their AliasOf property is changed.
			//This test exercises that behavior
			//
			// There are several permutations:
			//	* New item, new alias
			// 	* New item, stored alias
			// 	* Stored item, new alias
			//	* Stored item, stored alias
			//
			// Within each of these, the setting and clearing of AliasOf
			// must be tested

			//Start with new item, new alias
			SqliteCatalogStoreItem item = _store.CreateItem();
			item.Title = "Foo";
			item.Type = "fso";
			item.Uri = "foo/bar/baz";
			SqliteCatalogStoreItem alias = _store.CreateItem();
			alias.Title = "Foo Alias";
			alias.Type = "fso";
			alias.Uri = "foo/bar/baz/boo";
			Assert.IsFalse(item.Aliases.Contains(alias));
			alias.AliasOf = item;
			Assert.IsTrue(item.Aliases.Contains(alias));
			Assert.AreEqual(1, item.Aliases.Count);

			_store.AddItem(item);
			_store.AddItem(alias);

			//Re-load from the other store referencing this database
			item = _store2.GetItem(item.Id, true);
			alias = _store2.GetItem(alias.Id, true);
			Assert.AreEqual(item, alias.AliasOf);
			Assert.IsTrue(item.Aliases.Contains(alias));
			Assert.AreEqual(1, item.Aliases.Count);

			//Clear the AliasOf
			alias.AliasOf = null;
			Assert.IsFalse(item.Aliases.Contains(alias));
			Assert.AreEqual(0, item.Aliases.Count);

			//Reload from the original store
			item = _store.GetItem(item.Id, true);
			alias = _store.GetItem(alias.Id, true);
			Assert.IsNull(alias.AliasOf);
			Assert.IsFalse(item.Aliases.Contains(alias));
			Assert.AreEqual(0, item.Aliases.Count);

			//New item, stored alias
			alias = _store.GetItem(alias.Id);
			item = _store.CreateItem();
			item.Title = "Foo";
			item.Type = "fso";
			item.Uri = "foo/bar/baz";
			alias.AliasOf = item;
			Assert.IsTrue(item.Aliases.Contains(alias));
			Assert.AreEqual(1, item.Aliases.Count);
			//Test clearing this property before saving
			alias.AliasOf = null;
			Assert.IsFalse(item.Aliases.Contains(alias));
			Assert.AreEqual(0, item.Aliases.Count);
			//Put it back for the save
			alias.AliasOf = item;

			_store.AddItem(item);
			
			//Re-load from the other store referencing this database
			item = _store2.GetItem(item.Id, true);
			alias = _store2.GetItem(alias.Id, true);
			Assert.AreEqual(item, alias.AliasOf);
			Assert.IsTrue(item.Aliases.Contains(alias));
			Assert.AreEqual(1, item.Aliases.Count);

			//Stored item, new alias
			item = _store.GetItem(item.Id, true);
			//Clear out any aliases from the previous test
			//Note the use of a copy of the Aliases list to avoid iterating
			//over a list as items are deleted from it
			foreach (SqliteCatalogStoreItem oldAlias in new ArrayList(item.Aliases)) {
				_store.RemoveItem(oldAlias);
			}
			Assert.AreEqual(0, item.Aliases.Count);
			alias = _store.CreateItem();
			alias.Title = "Foo Alias";
			alias.Type = "fso";
			alias.Uri = "foo/bar/baz/boo";
			alias.AliasOf = item;
			Assert.IsTrue(item.Aliases.Contains(alias));
			Assert.AreEqual(1, item.Aliases.Count);

			//alias is new, so if item is loaded from the other store,
			//it should still show up as not having an alias at all
			item = _store2.GetItem(item.Id, true);
			Assert.IsFalse(item.Aliases.Contains(alias));
			Assert.AreEqual(0, item.Aliases.Count);

			//Re-set AliasOf and save
			alias.AliasOf = item;
			_store2.AddItem(alias);

			//Reload from the first store; this time it should come through
			item = _store.GetItem(item.Id, true);
			alias = _store.GetItem(alias.Id, true);
			Assert.IsTrue(item.Aliases.Contains(alias));
			Assert.AreEqual(1, item.Aliases.Count);
		}
	}
}
