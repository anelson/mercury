using System;
using System.Collections;
using System.IO;

using NUnit.Framework;

using Mercury.Core.CatalogStore;
using Mercury.Core.CatalogStore.Sqlite;

namespace Mercury.Tests.Core.CatalogStore.Sqlite
{
	[TestFixture]
	public class SqliteCatalogStoreTests
	{
		static SqliteCatalogStore _store = null;

		[SetUp]
		public void CreateStore() {
			SqliteCatalogStoreManager mgr = new SqliteCatalogStoreManager();
			_store = mgr.CreateCatalogStore(Path.GetTempFileName(), true);
		}

		[TearDown]
		public void RemoveStore() {
			String storePath = _store.Uri;
			_store.Dispose();
			_store = null;
			File.Delete(storePath);
		}

		[Test]
		public void UriTest() {
			String uri = _store.Uri;
			Assert.IsNotNull(uri);

			Assert.IsTrue(File.Exists(uri));
		}

		[Test]
		public void MgrTest() {
			//There's not much you can say about the Manager property, except
			//that it shouldn't be null
			Assert.IsNotNull(_store.Manager);
		}

		[Test]
		public void PropertiesTest() {
			Assert.IsNull(_store.GetProperty("foo bar"));
			_store.SetProperty("foo bar",  "baz");
			Assert.AreEqual("baz",  _store.GetProperty("foo bar"));

			//Open the store again with another store object, and ensure
			//this other instance can see the properties as well
			using (SqliteCatalogStore store2 = _store.Manager.OpenCatalogStore(_store.Uri)) {
				Assert.AreEqual("baz",  store2.GetProperty("foo bar"));
				store2.SetProperty("foo bar", null);
				Assert.IsNull(store2.GetProperty("foo bar"));
			}

			//The changes made in the other instance should also show up here
			Assert.IsNull(_store.GetProperty("foo bar"));
		}

		[Test]
		public void CreateItemTest() {
			SqliteCatalogStoreItem item = _store.CreateItem();
			Assert.IsNotNull(item);
			Assert.AreEqual(SqliteCatalogStoreItem.NULL_ID, 
							item.Id);
			Assert.AreEqual(null, 
							item.Uri);
			Assert.AreEqual(null,  
							item.Parent);
			Assert.AreEqual(null, 
							item.Title);
			Assert.AreEqual(0, 
							item.Aliases.Count);
			Assert.AreEqual(0, 
							item.Children.Count);
			Assert.AreEqual(0,  
							item.Tags.Count);
			Assert.AreEqual(_store, 
							item.Store);
		}

		[Test]
		public void AddItemTest() {
			//Create an item and add it to the store, with the expectation
			//that it can be retrieved again
			SqliteCatalogStoreItem item = _store.CreateItem();

			Assert.IsFalse(_store.ItemExists(item.Id));

			item.Title = "Test Item";
			item.Uri = @"c:\foo\bar\baz.zzz";
			item.Type = "fso.file";

			_store.AddItem(item);
			Assert.IsTrue(item.Id != SqliteCatalogStoreItem.NULL_ID);
			Assert.IsTrue(_store.ItemExists(item.Id));
		}

		[Test]
		public void GetItemTest() {
			//Get an added item from the store
			SqliteCatalogStoreItem item = _store.CreateItem();

			item.Title = "Test Item";
			item.Uri = @"c:\foo\bar\baz.zzz";
			item.Type = "fso.file";

			_store.AddItem(item);

			//The item should be cached, so the same instance is returned
			Assert.AreEqual(item, _store.GetItem(item.Id));
		}

		[Test]
		public void ForceLoadTest() {
			//Test the forced loading of an item
			//Add an item in one store, then change it in another
			//(so the change won't be reflected in _store's cache), 
			//then reload from _store and ensure the changes present
			SqliteCatalogStoreItem item = _store.CreateItem();

			item.Title = "Test Item";
			item.Uri = @"c:\foo\bar\baz.zzz";
			item.Type = "fso.file";

			_store.AddItem(item);

			using (SqliteCatalogStore store2 = _store.Manager.OpenCatalogStore(_store.Uri)) {
				SqliteCatalogStoreItem item2 = store2.GetItem(item.Id);

				item2.Title = "Changed Test Item";
			}

			SqliteCatalogStoreItem reloadedItem = _store.GetItem(item.Id, true);

			//reloadedItem should come from the cache, and thus be the same
			//object as item.
			Assert.AreEqual(item, reloadedItem);

			//And yet, the change to the title should be picked up
			Assert.AreEqual("Changed Test Item",
							item.Title);
		}

		[Test]
		public void PersistenceTest() {
			//Add an item, and load it from a new instance of the store, 
			//so it will be re-loaded from the database instead of the cache
			SqliteCatalogStoreItem item = _store.CreateItem();

			item.Title = "Test Item";
			item.Uri = @"c:\foo\bar\baz.zzz";
			item.Type = "fso.file";

			_store.AddItem(item);

			using (SqliteCatalogStore store2 = _store.Manager.OpenCatalogStore(_store.Uri)) {
				SqliteCatalogStoreItem item2 = store2.GetItem(item.Id);
	
				Assert.AreEqual(item.Id, item2.Id);
				Assert.AreEqual(item.Title, item2.Title);
				Assert.AreEqual(item.Uri, item2.Uri);
				Assert.AreEqual(item.Type, item2.Type);
				Assert.AreEqual(item.Parent, item2.Parent);
				Assert.AreEqual(item.AliasOf, item2.AliasOf);
			}
		}

		[Test]
		public void RemoveItemTest() {
			SqliteCatalogStoreItem item = _store.CreateItem();

			item.Title = "Test Item";
			item.Uri = @"c:\foo\bar\baz.zzz";
			item.Type = "fso.file";

			_store.AddItem(item);

			Assert.IsTrue(_store.ItemExists(item.Id));
			Assert.IsNotNull(_store.GetItem(item.Id));

			_store.RemoveItem(item);

			Assert.IsFalse(_store.ItemExists(item.Id));
		}

		[Test]
		public void CascadeRemoveItemTest() {
			//Removing an item should remove all its children and aliases as well
			SqliteCatalogStoreItem rootItem = _store.CreateItem();
			SqliteCatalogStoreItem level1ItemA = _store.CreateItem();
			SqliteCatalogStoreItem level1ItemAAlias = _store.CreateItem();
			SqliteCatalogStoreItem level1ItemB = _store.CreateItem();
			SqliteCatalogStoreItem level1ItemC = _store.CreateItem();
			SqliteCatalogStoreItem itemAChild1 = _store.CreateItem();
			SqliteCatalogStoreItem itemAChild2 = _store.CreateItem();
			SqliteCatalogStoreItem itemAChild3 = _store.CreateItem();

			rootItem.Title = "Root item";
			rootItem.Uri = @"rootItem";
			rootItem.Type = "fso.file";

			level1ItemA.Title = "Level 1 Item A";
			level1ItemA.Uri = @"level1ItemA";
			level1ItemA.Type = "fso.file";

			level1ItemAAlias.Title = "Level 1 Item A Alias";
			level1ItemAAlias.Uri = @"level1ItemAAlias";
			level1ItemAAlias.Type = "fso.file";

			level1ItemB.Title = "Level 1 Item B";
			level1ItemB.Uri = @"level1ItemB";
			level1ItemB.Type = "fso.file";

			level1ItemC.Title = "Level 1 Item C";
			level1ItemC.Uri = @"level1ItemC";
			level1ItemC.Type = "fso.file";

			itemAChild1.Title = "Item A Child 1";
			itemAChild1.Uri = @"itemAChild1";
			itemAChild1.Type = "fso.file";

			itemAChild2.Title = "Item A Child 2";
			itemAChild2.Uri = @"itemAChild2";
			itemAChild2.Type = "fso.file";

			itemAChild3.Title = "Item A Child 3";
			itemAChild3.Uri = @"itemAChild3";
			itemAChild3.Type = "fso.file";

			_store.AddItem(rootItem);

			level1ItemA.Parent = rootItem;
			_store.AddItem(level1ItemA);
			level1ItemB.Parent = rootItem;
			_store.AddItem(level1ItemB);
			level1ItemC.Parent = rootItem;
			_store.AddItem(level1ItemC);
			level1ItemAAlias.Parent = rootItem;
			level1ItemAAlias.AliasOf = level1ItemA;
			_store.AddItem(level1ItemAAlias);
			
			itemAChild1.Parent = level1ItemA;
			_store.AddItem(itemAChild1);
			itemAChild2.Parent = level1ItemA;
			_store.AddItem(itemAChild2);
			itemAChild3.Parent = level1ItemA;
			_store.AddItem(itemAChild3);

			Assert.IsTrue(_store.ItemExists(rootItem.Id));
			Assert.IsTrue(_store.ItemExists(level1ItemA.Id));
			Assert.IsTrue(_store.ItemExists(level1ItemAAlias.Id));
			Assert.IsTrue(_store.ItemExists(level1ItemB.Id));
			Assert.IsTrue(_store.ItemExists(level1ItemC.Id));
			Assert.IsTrue(_store.ItemExists(itemAChild1.Id));
			Assert.IsTrue(_store.ItemExists(itemAChild2.Id));
			Assert.IsTrue(_store.ItemExists(itemAChild3.Id));

            //Remove item a.  This should cascade delete item a's alias,
			//and item a's 3 children, but leave the root, b, and c.
			_store.RemoveItem(level1ItemA);

			Assert.IsTrue(_store.ItemExists(rootItem.Id));
			Assert.IsFalse(_store.ItemExists(level1ItemA.Id));
			Assert.IsFalse(_store.ItemExists(level1ItemAAlias.Id));
			Assert.IsTrue(_store.ItemExists(level1ItemB.Id));
			Assert.IsTrue(_store.ItemExists(level1ItemC.Id));
			Assert.IsFalse(_store.ItemExists(itemAChild1.Id));
			Assert.IsFalse(_store.ItemExists(itemAChild2.Id));
			Assert.IsFalse(_store.ItemExists(itemAChild3.Id));
		}

		[Test]
		public void RemoveAliasTest() {
			//Removing an alias to an item should remove that alias from
			//the item's alias list
			SqliteCatalogStoreItem item = _store.CreateItem();
			SqliteCatalogStoreItem alias = _store.CreateItem();

			item.Title = "Level 1 Item A";
			item.Uri = @"level1ItemA";
			item.Type = "fso.file";

			alias.Title = "Level 1 Item A Alias";
			alias.Uri = @"level1ItemAAlias";
			alias.Type = "fso.file";
			alias.AliasOf = item;

			_store.AddItem(item);
			_store.AddItem(alias);

			Assert.IsTrue(item.Aliases.Contains(alias));
			Assert.AreEqual(1, item.Aliases.Count);

			//Remove the alias from the store
			_store.RemoveItem(alias);

			Assert.IsFalse(item.Aliases.Contains(item));
			Assert.AreEqual(0, item.Aliases.Count);
		}

		[Test]
		public void BatchAddTest() {
			//Add a couple items as part of a batch
			_store.BeginBatch();
			
			SqliteCatalogStoreItem item = _store.CreateItem();

			item.Title = "Test Item";
			item.Uri = @"c:\foo\bar\baz.zzz";
			item.Type = "fso.file";

			_store.AddItem(item);	
			Assert.IsTrue(_store.ItemExists(item.Id));		

			item = _store.CreateItem();

			item.Title = "Test Item 2";
			item.Uri = @"c:\foo\bar\baz2.zzz";
			item.Type = "fso.file";

			_store.AddItem(item);  	
			Assert.IsTrue(_store.ItemExists(item.Id));		

			item = _store.CreateItem();

			item.Title = "Test Item 3";
			item.Uri = @"c:\foo\bar\baz3.zzz";
			item.Type = "fso.file";

			_store.AddItem(item);
			Assert.IsTrue(_store.ItemExists(item.Id));		

			_store.EndBatch();

			//The most recently added item (and hopefully the other two)
			//still exists after committing the batch
			Assert.IsTrue(_store.ItemExists(item.Id));		
		}

		[Test]
		public void GetRootItemsTest() {
			//A fresh store has no items
			SqliteCatalogStoreItemCollection col = _store.GetRootItems();
			Assert.AreEqual(0, col.Count);

			//Add a root item
			SqliteCatalogStoreItem item = _store.CreateItem();

			item.Title = "Test Item";
			item.Uri = @"c:\foo\bar\baz.zzz";
			item.Type = "fso.file";

			_store.AddItem(item);
			
			col = _store.GetRootItems();
			Assert.AreEqual(1, col.Count);	

			//Add a child to the root item; shouldn't make a difference
			SqliteCatalogStoreItem child = _store.CreateItem();

			child.Title = "Test Item";
			child.Uri = @"c:\foo\bar\baz.zzz";
			child.Type = "fso.file";
			child.Parent = item;

			_store.AddItem(child);
			
			col = _store.GetRootItems();
			Assert.AreEqual(1, col.Count);

			//Delete the item; should be no root items
			_store.RemoveItem(item);
			col = _store.GetRootItems();
			Assert.AreEqual(0, col.Count);
		}
	}
}
