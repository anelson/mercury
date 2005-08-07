using System;
using System.Data;
using System.Collections;

using Finisar.SQLite;

using Mercury.Core.CatalogStore;
using Mercury.Util;

namespace Mercury.Core.CatalogStore.Sqlite
{
	[Flags]
	/// <summary>Flags corresponding to each element in a catalog item.  These flags are used
	///     to denote which elements of an item have changed and therefore require an update.</summary>
		internal enum StoreItemElements {
		Uri = 0x0,
		Title = 0x1,
		Tags = 0x2,
		Parent = 0x4,
		AliasOf = 0x8,
		Childen = 0x10,
		Aliases = 0x20,
		Type = 0x40
	};

	/// <summary>Very simple and purpose-built persistor to save catalog items to the database
	///     and retrieve them therefrom.</summary>
	internal class ItemPersistor {
		SQLiteConnection _conn;
		SqliteCatalogStore _store;
		Hashtable _wordIdHash;

		public ItemPersistor(SqliteCatalogStore store, SQLiteConnection conn) {
			_store = store;
			_conn = conn;
			_wordIdHash = new Hashtable();
		}

        /// <summary>Loads a previously-saved catalog store item</summary>
        /// 
        /// <param name="item"></param>
        /// 
        /// <returns>true if item found and loaded; false if item does not exist in database</returns>
		public bool GetItem(SqliteCatalogStoreItem item) {
			try {
				item.Persisting = true;

				//First, retrieve the basic catalog info
				String sql = @"
							 select uri, title, type, alias_catalog_item_id
					from catalog_items
					where catalog_item_id = ?";

				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;

					cmd.CreateAndAddUnnamedParameters();

					cmd.Parameters[0].Value = item.Id;

					SQLiteDataReader rdr = cmd.ExecuteReader();

					if (!rdr.Read()) {
						//The ID wasn't found.  Hmm.
						rdr.Close();
						return false;
					}

					item.Uri = rdr.GetString(0);
					item.Title = rdr.GetString(1);
					item.Type = rdr.GetString(2);

					if (rdr.IsDBNull(3)) {
						//Alias-of is null
						item.AliasOf = null;
					} else {
						//Alias isn't null.  Use the store to get this
						//item.  It'll either come from the cache (asymptotic to free)
						//or be created with loading deferred (still cheap)
						item.AliasOf = _store.GetItem(rdr.GetInt64(3));
					}
					rdr.Close();
				}

				//Now retrieve the item tags
				sql = @"
					  select tag from catalog_item_tags where catalog_item_id = ?";
				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;

					cmd.CreateAndAddUnnamedParameters();

					cmd.Parameters[0].Value = item.Id;

					SQLiteDataReader rdr = cmd.ExecuteReader();

					item.Tags.Clear();

					while (rdr.Read()) {
						item.Tags.Add(rdr.GetString(0));
					}

					rdr.Close();
				}

				//And children
				sql = @"
					  select catalog_item_id from catalog_items where parent_catalog_item_id = ?";
				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;

					cmd.CreateAndAddUnnamedParameters();

					cmd.Parameters[0].Value = item.Id;

					SQLiteDataReader rdr = cmd.ExecuteReader();

					item.Children.Clear();

					while (rdr.Read()) {
						item.Children.Add(_store.GetItem(rdr.GetInt64(0)));
					}

					rdr.Close();
				}

				//And aliases
				sql = @"
					  select catalog_item_id from catalog_items where alias_catalog_item_id = ?";
				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;

					cmd.CreateAndAddUnnamedParameters();

					cmd.Parameters[0].Value = item.Id;

					SQLiteDataReader rdr = cmd.ExecuteReader();

					item.Aliases.Clear();

					while (rdr.Read()) {
						item.Aliases.Add(_store.GetItem(rdr.GetInt64(0)));
					}

					rdr.Close();
				}           
			} finally {
				item.Persisting = false;
			}

			return true;
		}

        /// <summary>Tests that an item with a given ID exists in the store.</summary>
        /// 
        /// <param name="id"></param>
        /// 
        /// <returns></returns>
		public bool ItemExists(long id) {
			String sql = @"
			select catalog_item_id
				from catalog_items
				where catalog_item_id = ?";

			using (SQLiteCommand cmd = _conn.CreateCommand()) {
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = sql;

				cmd.CreateAndAddUnnamedParameters();

				cmd.Parameters[0].Value = id;

				Object result = cmd.ExecuteScalar();

				return (result != null);
			}
		}

		/// <summary>Adds an item not already in the store</summary>
		/// 
		/// <param name="item"></param>
		public void AddItem(SqliteCatalogStoreItem item) {
			if (item.Stored) {
				throw new ArgumentException("Item has already been added to store",  "item");
			}

			try {
				item.Persisting = true;

				String sql = @"
							 insert into catalog_items
							 (
							 parent_catalog_item_id,
							 uri,
							 title,
							 type,
							 alias_catalog_item_id
							 ) 
							 values(?, ?, ?, ?, ?)";

				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;
					cmd.CreateAndAddUnnamedParameters();

					cmd.Parameters[0].DbType = DbType.Int64;
					if (item.Parent == null || !item.Parent.Stored) {
						cmd.Parameters[0].Value = DBNull.Value;
					} else {
						cmd.Parameters[0].Value = item.Parent.Id;
					}

					cmd.Parameters[1].DbType = DbType.String;
					cmd.Parameters[1].Value = item.Uri;

					cmd.Parameters[2].DbType = DbType.String;
					cmd.Parameters[2].Value = item.Title;

					cmd.Parameters[3].DbType = DbType.String;
					cmd.Parameters[3].Value = item.Type;

					cmd.Parameters[4].DbType = DbType.Int64;
					if (item.AliasOf == null || !item.AliasOf.Stored) {
						cmd.Parameters[4].Value = DBNull.Value;
					} else {
						cmd.Parameters[4].Value = item.AliasOf.Id;
					}

					cmd.ExecuteNonQuery();

					item.Id = cmd.Connection.GetLastInsertRowId();
				}

				//Add the words in this title to the word graph
				SetItemTitleWords(item);

				//Add tags
				sql = "insert into catalog_item_tags(catalog_item_id, tag) values (?, ?)";
				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;
					cmd.CreateAndAddUnnamedParameters();
					cmd.Prepare();
					cmd.Parameters[0].Value = item.Id;

					foreach (String tag in item.Tags) {
						cmd.Parameters[1].Value = tag;
						cmd.ExecuteNonQuery();
					}
				}

				//The item has now been loaded
				item.Loaded = true;

				//Update the references to this item from its aliases and children
				foreach (SqliteCatalogStoreItem alias in item.Aliases) {
					//Update this item's AliasOf property so it will update
					//again, this time w/ the correct ID
					alias.AliasOf = item;
				}

				foreach (SqliteCatalogStoreItem child in item.Children) {
					//update this item's Parent property to reflect
					//the newly assigned ID of this item.
					child.Parent = item;
				}
			} finally {
				item.Persisting = false;
			}
		}

		/// <summary>Updates the database to reflect the new values of the changed catalog item elements.</summary>
		/// 
		/// <param name="item">The item to update in the databsse</param>
		/// <param name="updatedElements">The elements of the item that have changed and therefore should be updated.</param>
		public void UpdateItem(SqliteCatalogStoreItem item, StoreItemElements updatedElements) {
			if (!item.Stored) {
				throw new ArgumentException("Cannot update an item that has not been saved to the catalog", "item");
			}

			if (!item.Loaded) {
				throw new ArgumentException("Cannot update an item that has not been loaded", "item");
			}
			try {
				item.Persisting = true;
				
				//If any of the simple elements of the catalog_items table have
				//changed, update them.  It's easier to just update them all
				if ((updatedElements & (StoreItemElements.Title | 
										StoreItemElements.Uri | 
										StoreItemElements.Type |
										StoreItemElements.AliasOf | 
										StoreItemElements.Parent)) != 0) {
					String sql = @"
								 update catalog_items
								 set 
								 parent_catalog_item_id = ?,
						uri = ?,
						title = ?,
						type = ?,
						alias_catalog_item_id = ?
												where
												catalog_item_id = ?";

					using (SQLiteCommand cmd = _conn.CreateCommand()) {
						cmd.CommandType = CommandType.Text;
						cmd.CommandText = sql;
						cmd.CreateAndAddUnnamedParameters();

						cmd.Parameters[0].DbType = DbType.Int64;
						if (item.Parent == null || !item.Parent.Stored) {
							cmd.Parameters[0].Value = DBNull.Value;
						} else {
							cmd.Parameters[0].Value = item.Parent.Id;
						}

						cmd.Parameters[1].DbType = DbType.String;
						cmd.Parameters[1].Value = item.Uri;

						cmd.Parameters[2].DbType = DbType.String;
						cmd.Parameters[2].Value = item.Title;

						cmd.Parameters[3].DbType = DbType.String;
						cmd.Parameters[3].Value = item.Type;

						cmd.Parameters[4].DbType = DbType.Int64;
						if (item.AliasOf == null || !item.AliasOf.Stored) {
							cmd.Parameters[4].Value = DBNull.Value;
						} else {
							cmd.Parameters[4].Value = item.AliasOf.Id;
						}
						cmd.Parameters[5].DbType = DbType.Int64;
						cmd.Parameters[5].Value = item.Id;

						cmd.ExecuteNonQuery();
					}

					//If the title has changed, need to rebuild the title graph too
					if ((updatedElements & StoreItemElements.Title) != 0) {
						SetItemTitleWords(item);
					}
				}

				//If the tags changed, update those
				if ((updatedElements & StoreItemElements.Tags) != 0) {
					//Delete existing tags, and replace with the new tags
					//Realistically, it would be a trivial optimization to 
					//just update changed tags, delete removed, etc.
					String sql = @"delete from catalog_item_tags where catalog_item_id = ?";

					using (SQLiteCommand cmd = _conn.CreateCommand()) {
						cmd.CommandType = CommandType.Text;
						cmd.CommandText = sql;
						cmd.CreateAndAddUnnamedParameters();
						cmd.Parameters[0].DbType = DbType.Int64;
						cmd.Parameters[0].Value = item.Id;

						cmd.ExecuteNonQuery();
					}

					//Now re-create the tags
					sql = "insert into catalog_item_tags(catalog_item_id, tag) values (?, ?)";
					using (SQLiteCommand cmd = _conn.CreateCommand()) {
						cmd.CommandType = CommandType.Text;
						cmd.CommandText = sql;
						cmd.CreateAndAddUnnamedParameters();
						cmd.Prepare();
						cmd.Parameters[0].Value = item.Id;

						foreach (String tag in item.Tags) {
							cmd.Parameters[1].Value = tag;
							cmd.ExecuteNonQuery();
						}
					}               
				}

				//If the alias or children lists change, that's a special case.
				//it must mean that another item has changed its AliasOf or
				//Parent property.  Since the children and aliases lists are
				//build from the Parent and AliasOf properties, respectively,
				//there's no need to explicitly save these lists; only the items within
				//them.
			} finally {
				item.Persisting = false;
			}
		}

        /// <summary>Removes an item from the catalog, recursively deleting its children and aliases
        ///     as well.</summary>
        /// 
        /// <param name="item"></param>
		public void RemoveItem(SqliteCatalogStoreItem item) {
			if (!item.Stored) {
				throw new ArgumentException("Cannot delete items not stored in the catalog", "item");
			}

			try {
				item.Persisting = true;

				//Delete any children of this item
				foreach (SqliteCatalogStoreItem child in item.Children) {
					if (child.Stored) {
						RemoveItem(child);
					} else {
						//Else, item isn't stored yet so nothing to delete
						//Null out its parent
						child.Parent = null;
					}
				}

				//Delete aliases to this item
				foreach (SqliteCatalogStoreItem alias in item.Aliases) {
					if (alias.Stored) {
						RemoveItem(alias);
					} else {
						//Else, item isn't stored yet so nothing to delete
						//Null out its AliasOf
						alias.AliasOf = null;
					}
				}

				//Delete all the item's tags
				String sql = @"delete from catalog_item_tags where catalog_item_id = ?";

				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;

					cmd.CreateAndAddUnnamedParameters();

					cmd.Parameters[0].Value = item.Id;

					cmd.ExecuteNonQuery();
				}

				//Delete the item's mapping to title word graph nodes
				sql = @"delete from title_word_graph_node_items where catalog_item_id = ?";

				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;

					cmd.CreateAndAddUnnamedParameters();

					cmd.Parameters[0].Value = item.Id;

					cmd.ExecuteNonQuery();
				}

				//Delete the title word graph nodes that have no catalog items associated with them
				sql = @"
delete from 
title_word_graph
where node_id in (
	select twg.node_id	
	from														
	title_word_graph twg
	left join
	title_word_graph_node_items twgni
	on
	twg.node_id = twgni.node_id
	where twgni.node_id is null)";

				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;

					cmd.ExecuteNonQuery();
				}

				//Delete the title word graph descendants that refer to nodes no longer in existence
				sql = @"
delete from 
title_word_graph_node_dscndnts
where node_id in (
	select twgd.node_id
	from														   
	title_word_graph_node_dscndnts twgd
	left join
	title_word_graph twg
	on
	twgd.node_id = twg.node_id
	or
	twgd.descendant_node_id = twg.node_id
	where twg.node_id is null)";

				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;

					cmd.ExecuteNonQuery();
				}


				//Delete the item itself
				sql = @"delete from catalog_items where catalog_item_id = ?";

				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;

					cmd.CreateAndAddUnnamedParameters();

					cmd.Parameters[0].Value = item.Id;

					cmd.ExecuteNonQuery();
				}

				//If item is alias, remove item from AliasOf's alias list
				if (item.AliasOf != null) {
					item.AliasOf.Aliases.Remove(item);
				}

				if (item.Parent != null) {
					item.Parent.Children.Remove(item);
				}

				//Item is no longer stored
				item.Loaded = false;
				item.Id = SqliteCatalogStoreItem.NULL_ID;
				item.Title = null;
				item.Type = null;
				item.Uri = null;
				item.Parent = null;
				item.AliasOf = null;
				item.Aliases.Clear();
				item.Children.Clear();
			} finally {
				item.Persisting = false;
			}
		}

        /// <summary>Gets all items in the root of the store.</summary>
        /// 
        /// <returns></returns>
		public SqliteCatalogStoreItemCollection GetRootItems() {
			SqliteCatalogStoreItemCollection col = new SqliteCatalogStoreItemCollection();
			
			String sql = @"
				  select catalog_item_id from catalog_items where parent_catalog_item_id is null";
			using (SQLiteCommand cmd = _conn.CreateCommand()) {
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = sql;

				SQLiteDataReader rdr = cmd.ExecuteReader();

				while (rdr.Read()) {
					col.Add(_store.GetItem(rdr.GetInt64(0)));
				}

				rdr.Close();
			}

			return col;
		}

		public String GetProperty(String name) {
			String sql = @"select value from store_properties where name = ?";

			using (SQLiteCommand cmd = _conn.CreateCommand()) {
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = sql;

				cmd.CreateAndAddUnnamedParameters();

				cmd.Parameters[0].Value = name;

				Object val = cmd.ExecuteScalar();

				return (String)val;
			}
		}

		public void SetProperty(String name, String value) {
			if (value == null) {
				//Clear this property
				String sql = @"delete from store_properties where name = ?";

				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;

					cmd.CreateAndAddUnnamedParameters();

					cmd.Parameters[0].Value = name;

					cmd.ExecuteNonQuery();
				}
			} else {
				//Else, set the property.  Insert if it doesn't exist; update
				//if it does
				if (GetProperty(name) != null) {
					//Property already exists
					String sql = @"update store_properties set value = ? where name = ?";

					using (SQLiteCommand cmd = _conn.CreateCommand()) {
						cmd.CommandType = CommandType.Text;
						cmd.CommandText = sql;

						cmd.CreateAndAddUnnamedParameters();

						cmd.Parameters[0].Value = value;
						cmd.Parameters[1].Value = name;

						cmd.ExecuteNonQuery();
					}
				} else {
					//Doesn't exist; create
					String sql = @"insert into store_properties(name, value) values(?, ?)";

					using (SQLiteCommand cmd = _conn.CreateCommand()) {
						cmd.CommandType = CommandType.Text;
						cmd.CommandText = sql;

						cmd.CreateAndAddUnnamedParameters();

						cmd.Parameters[0].Value = name;
						cmd.Parameters[1].Value = value;

						cmd.ExecuteNonQuery();
					}
				}
			}
		}

		/// <summary>Looks up the ID of a word in the database, adding it if not found.</summary>
		/// 
		/// <param name="word"></param>
		/// <param name="addToList"></param>
		/// 
		/// <returns></returns>
		private long GetWordId(String word) {
			//Queries the table of title words to get the numeric ID assigned to this word
			//If the word is not found, adds it
			if (_wordIdHash.Contains(word)) {
				return(long)_wordIdHash[word];
			}

			//Word isn't in the word ID hash.  Check for it in the database
			String sql = @"select word_id from title_words where word = ?";
			long wordId = SqliteCatalogStoreItem.NULL_ID;

			using (SQLiteCommand cmd = _conn.CreateCommand()) {
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = sql;
				cmd.CreateAndAddUnnamedParameters();

				cmd.Parameters[0].DbType = DbType.String;
				cmd.Parameters[0].Value = word;

				Object result = cmd.ExecuteScalar();
				if (result != null) {
					//Found the word; it just wasn't in the cache
					wordId = (long)result;
				}
			}

			if (wordId == SqliteCatalogStoreItem.NULL_ID) {
				//Word isn't in the database; add it
				sql = @"insert into title_words(word, one_chars, two_chars, three_chars, four_chars, five_chars) 
					  values(?, ?, ?, ?, ?, ?)";
				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;
					cmd.CreateAndAddUnnamedParameters();

					cmd.Parameters[0].DbType = DbType.String;
					cmd.Parameters[0].Value = word;

					cmd.Parameters[1].DbType = DbType.String;
					cmd.Parameters[1].Value = word.Substring(0, 1);

					if (word.Length > 1) {
						cmd.Parameters[2].DbType = DbType.String;
						cmd.Parameters[2].Value = word.Substring(0, 2);

						if (word.Length > 2) {
							cmd.Parameters[3].DbType = DbType.String;
							cmd.Parameters[3].Value = word.Substring(0, 3);

							if (word.Length > 3) {
								cmd.Parameters[4].DbType = DbType.String;
								cmd.Parameters[4].Value = word.Substring(0, 4);

								if (word.Length > 4) {
									cmd.Parameters[5].DbType = DbType.String;
									cmd.Parameters[5].Value = word.Substring(0, 5);
								}
							}
						}
					}

					cmd.ExecuteNonQuery();

					wordId = cmd.Connection.GetLastInsertRowId();
				}
			}

			//One way or another, have a word ID now
			_wordIdHash.Add(word,  wordId);

			return wordId;
		}

		/// <summary>Rebuilds the title word graph entries for this node.</summary>
		/// 
		/// <param name="item"></param>
		private void SetItemTitleWords(SqliteCatalogStoreItem item) {
			//Break the title down into tokens
			String[] titleTokens = _store.Manager.Tokenizer.TokenizeTitle(item.Title);

			//Delete any graph nodes for this catalog item
			//TODO: Need logic to delete the graph nodes themselves when
			//they are no longer associated with any catalog items
			String sql = @"
						 delete from title_word_graph_node_items 
						 where
						 catalog_item_id = ?";
			using (SQLiteCommand cmd = _conn.CreateCommand()) {
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = sql;
				cmd.CreateAndAddUnnamedParameters();

				cmd.Parameters[0].DbType = DbType.Int64;
				cmd.Parameters[0].Value = item.Id;

				cmd.ExecuteNonQuery();
			}

			//Now, (re)create the title word and title graph entries for this
			//title
			long prevNodeId = SqliteCatalogStoreItem.NULL_ID, 
				wordId = SqliteCatalogStoreItem.NULL_ID;

			//Prepare the command used to add a row in the title_word_graph_node_items associating
			//each of the title word graph nodes along this title, with this catalog item
			using (SQLiteCommand cmd = _conn.CreateCommand()) {
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = "insert into title_word_graph_node_items(node_id, catalog_item_id) values (?, ?)";
				cmd.CreateAndAddUnnamedParameters();
				cmd.Prepare();
				cmd.Parameters[1].Value = item.Id;

				for (int idx = 0; idx < titleTokens.Length; idx++) {
					wordId = GetWordId(titleTokens[idx]);

					long nodeId = SetTitleWordGraphNode(wordId, prevNodeId);

					cmd.Parameters[0].Value = nodeId;
					cmd.ExecuteNonQuery();

					prevNodeId = nodeId;
				}
			}
		}

		/// <summary>Creates the title word graph node for the given word and ordinal position.
		///     If the node already exists, does nothing.</summary>
		/// 
		/// <param name="wordId"></param>
		/// <param name="prevNodeId"></param>
		/// 
		/// <returns>The ID of the title word graph node created</returns>
		private long SetTitleWordGraphNode(long wordId, long prevNodeId) {
			long nodeId = SqliteCatalogStoreItem.NULL_ID;

			//First, check if this node already exists
			String sql = "select node_id from title_word_graph where word_id = ? ";
			if (prevNodeId == SqliteCatalogStoreItem.NULL_ID) {
				sql += " and prev_node_id is null";
			} else {
				sql += " and prev_node_id = ?";
			}

			using (SQLiteCommand cmd = _conn.CreateCommand()) {
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = sql;
				cmd.CreateAndAddUnnamedParameters();

				cmd.Parameters[0].Value = wordId;
				if (prevNodeId != SqliteCatalogStoreItem.NULL_ID) {
					cmd.Parameters[1].Value = prevNodeId;
				}

				Object result = cmd.ExecuteScalar();

				if (result != null) {
					nodeId = (long)result;
				}
			}

			if (nodeId == SqliteCatalogStoreItem.NULL_ID) {
				//This node doesn't exist; create it
				sql = "insert into title_word_graph(word_id, prev_node_id) values (?,?)";

				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = sql;
					cmd.CreateAndAddUnnamedParameters();
					cmd.Parameters[0].Value = wordId;
					if (prevNodeId == SqliteCatalogStoreItem.NULL_ID) {
						cmd.Parameters[1].Value = DBNull.Value;
					} else {
						cmd.Parameters[1].Value = prevNodeId;
					}

					cmd.ExecuteNonQuery();

					nodeId = cmd.Connection.GetLastInsertRowId();
				}

				//Add this node to the list of descendants for all of 
				//the previous node's ancestors
				if (prevNodeId != SqliteCatalogStoreItem.NULL_ID) {
					AddDescendantNode(prevNodeId, nodeId);
				}
			}

			return nodeId;
		}

		/// <summary>Adds nodeId to the list of descendants for prevNodeId, then
		///     looks up prevNodeId's parent and add's nodeId to the parent's list of descendants,
		///     and so on, recursively up to the root</summary>
		/// 
		/// <param name="prevNodeId"></param>
		/// <param name="nodeId"></param>
		private void AddDescendantNode(long prevNodeId, long nodeId) {
			String sql = "insert into title_word_graph_node_dscndnts(node_id, descendant_node_id) values (?,?)";

			using (SQLiteCommand cmd = _conn.CreateCommand()) {
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = sql;
				cmd.CreateAndAddUnnamedParameters();

				cmd.Parameters[0].Value = prevNodeId;
				cmd.Parameters[1].Value = nodeId;

				cmd.ExecuteNonQuery();
			}

			//Ok, nodeId added to prevNodeId's descendants.  Now find prevNodeId's parent and add nodeId there
			sql = "select prev_node_id from title_word_graph where node_id = ?";
			using (SQLiteCommand cmd = _conn.CreateCommand()) {
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = sql;
				cmd.CreateAndAddUnnamedParameters();

				cmd.Parameters[0].Value = prevNodeId;

				Object result = cmd.ExecuteScalar();

				if (result != DBNull.Value) {
					prevNodeId = (long)result;

					AddDescendantNode(prevNodeId, nodeId);
				}
			}
		}
	}
}
