using System;
using System.Collections;

using Finisar.SQLite;

using Mercury.Core.CatalogStore;

namespace Mercury.Core.CatalogStore.Sqlite
{
	public class SqliteCatalogStore : ICatalogStore
	{
        /// <summary>The DB connection string for the database underlying this store.  Each thread
        ///     gets its own connection object, but the connection string is global.</summary>
		String _connStr;

		[ThreadStatic]
        /// <summary>Thread-specific database connection object.  Since the sqlite3 API doesn't support
        ///     sharing a single connection across threads, each thread must maintain its own
        ///     connection.</summary>
		SQLiteConnection _conn = null;

		[ThreadStatic]
        /// <summary>The current transaction for the thread's connection.  Between calls to 
        ///     BeginBatch() and EndBatch(), this variable stores the transaction object.</summary>
		SQLiteTransaction _tx = null;

		[ThreadStatic]
        /// <summary>Because the ItemPersistor is connection-specific, it also must be thread-local.</summary>
		ItemPersistor _persistor = null;

        /// <summary>The cache of already-loaded items, keyed by ID.</summary>
		Hashtable _itemCache;
		
		String _uri;
		SqliteCatalogStoreManager _mgr;
		
        /// <summary>Uses an existing connection to a SQLite database created as a catalog store.
        ///     Assumes the schema has already been populated.</summary>
        /// 
        /// <param name="conn"></param>
		internal SqliteCatalogStore(SqliteCatalogStoreManager mgr, SQLiteConnection conn, String uri)
		{
			if (conn == null) {
				throw new ArgumentNullException("conn");
			}

			_mgr = mgr;
			//Use this connection for the constructing thread
			//It's reasonable to assume this thread will be doing most
			//of the work w/ the store
			_conn = conn;
			_uri = uri;

			_connStr = conn.ConnectionString;

			_itemCache = new Hashtable();
		}

		#region ICatalogStore Members

		ICatalogStoreManager ICatalogStore.Manager {
			get {
				return this.Manager;
			}
		}
		
		public string Uri {
			get {
				return _uri;
			}
		}

        /// <summary>Starts a batch load/update of the store.  Batching allows caching and can improve
        ///     performance.</summary>
		public void BeginBatch() {
			if (_tx != null) {
				throw new InvalidOperationException("Cannot call BeginBatch while a previous batch is still pending");
			}

			_tx = Connection.BeginTransaction();
		}
		
		public void EndBatch() {
			if (_tx == null) {
				throw new InvalidOperationException("There is no outstanding batch on this thread");
			}

			_tx.Commit();
			_tx.Dispose();
			_tx = null;
		}

		void ICatalogStore.RemoveItem(ICatalogStoreItem item) {
			this.RemoveItem((SqliteCatalogStoreItem)item);
		}

		ICatalogStoreItem ICatalogStore.CreateItem() {
			return this.CreateItem();
		}

		void ICatalogStore.AddItem(ICatalogStoreItem item) {
			this.AddItem((SqliteCatalogStoreItem)item);
		}

		ICatalogStoreItem ICatalogStore.GetItem(long id) {
			return this.GetItem(id);
		}
		
		ICatalogStoreItem ICatalogStore.GetItem(long id, bool forceLoad) {
			return this.GetItem(id, forceLoad);
		}

		public bool ItemExists(long id) {
			return Persistor.ItemExists(id);
		}

		ICatalogStoreItemCollection ICatalogStore.GetRootItems() {
			return this.GetRootItems();
		}

		public String GetProperty(String name) {
			return Persistor.GetProperty(name);
		}

		public void SetProperty(String name, String value) {
			Persistor.SetProperty(name, value);
		}

		#endregion

		public SqliteCatalogStoreManager Manager {
			get {
				return _mgr;
			}
		}

		public void RemoveItem(SqliteCatalogStoreItem item) {
			long itemId = item.Id;
			Persistor.RemoveItem(item);
			lock (_itemCache) {
				_itemCache.Remove(itemId);
			}
		}

		public SqliteCatalogStoreItem CreateItem() {
			return new SqliteCatalogStoreItem(this);
		}

		public SqliteCatalogStoreItem GetItem(long id) {
			return GetItem(id, false);
		}
		
		public SqliteCatalogStoreItem GetItem(long id, bool forceLoad) {
			//Check the cache for this item.  If found, re-use the cache
			//item
			lock (_itemCache) {
				if (_itemCache.Contains(id)) {
					//Item already in the cache.  if forceLoad, reload
					//from the database
					SqliteCatalogStoreItem item = (SqliteCatalogStoreItem)_itemCache[id];

					if (forceLoad) {
						Persistor.GetItem(item);
					}

					return item;
				} else {
					//Create a new item associated with this ID, but don't load
					//right away, to save DB IO
					if (!Persistor.ItemExists(id)) {
						throw new ArgumentException(String.Format("No item with ID {0} exists in this catalog", 
																  id), 
													"id");
					}
					
					SqliteCatalogStoreItem item = new SqliteCatalogStoreItem(this, id);
					_itemCache[id] = item;

					return item;
				}
			}
		}

		public void AddItem(SqliteCatalogStoreItem item) {
			Persistor.AddItem(item);

			//Put this item in the cache
			lock (_itemCache) {
				_itemCache.Add(item.Id, item);
			}
		}

		public SqliteCatalogStoreItemCollection GetRootItems() {
			return Persistor.GetRootItems();
		}

		#region IDisposable Members

		public void Dispose() {
			if (_conn != null) {
				_conn.Close();
				_conn.Dispose();
				_conn = null;
			}
		}

		#endregion

        /// <summary>The connection to the SQLite database.  If there is no connection for this thread,
        ///     opens one.</summary>
		private SQLiteConnection Connection {
			get {
				if (_conn == null) {
					_conn = new SQLiteConnection(_connStr);
					_conn.Open();
				}

				return _conn;
			}
		}

		internal ItemPersistor Persistor {
			get {
				if (_persistor == null) {
					_persistor = new ItemPersistor(this, Connection);
				}

				return _persistor;
			}
		}
	}
}
