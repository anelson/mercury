using System;

using Finisar.SQLite;

using Mercury.Core.CatalogStore;
using Mercury.Util;

namespace Mercury.Core.CatalogStore.Sqlite
{
	/// <summary>
	/// Summary description for SqliteCatalogStoreItem.
	/// </summary>
	public class SqliteCatalogStoreItem : ICatalogStoreItem
	{
		public const long NULL_ID = -1;

		long _id;
		SqliteCatalogStore _store;
		bool _loaded;
		bool _persisting;

		String _uri;
		String _title;
		String _type;
		StringCollection _tags;
		SqliteCatalogStoreItem _parent, _aliasOf;
		SqliteCatalogStoreItemCollection _aliases, _children;		

		internal SqliteCatalogStoreItem(SqliteCatalogStore store) : this(store, NULL_ID)
		{
		}
		
		internal SqliteCatalogStoreItem(SqliteCatalogStore store, long itemId)
		{
			_store = store;
			_id = itemId;
			_loaded = false;
			_persisting = false;

			_uri = null;
			_title = null;
			_type = null;
			_tags = new StringCollection();
			_parent = null;
			_aliasOf = null;
			_aliases = new SqliteCatalogStoreItemCollection();
			_children = new SqliteCatalogStoreItemCollection();
		}

        /// <summary>True if this item is stored in the catalog.  false if it has not been added
        ///     to a catalog store since being created.</summary>
		internal bool Stored {
			get {
				//Stored catalog items have an ID; non-stored ones don't
				return _id != NULL_ID;
			}
		}

        /// <summary>Internal flag set by the ItemPersistor when this item is being loaded, added,
        ///     updated, or deleted.  When this flag is set, the item ignores all changes to its
        ///     properties on the assumption that the ItemPersistor knows best.</summary>
		internal bool Persisting {
			get {
				return _persisting;
			}

			set {
				_persisting = value;
			}
		}

		internal bool Loaded {
			get {
				return _loaded;
			}

			set {
				_loaded = value;
			}
		}
		
		#region ICatalogStoreItem Members

		public string Uri {
			get {
				Load();
				return _uri;
			}
			set {
				Load();
				_uri = value;
				UpdateElement(StoreItemElements.Uri);
			}
		}

		public string Title {
			get {
				Load();
				return _title;
			}
			set {
				Load();
				_title = value;
				UpdateElement(StoreItemElements.Title);
			}
		}

		public string Type {
			get {
				Load();
				return _type;
			}
			set {
				Load();
				_type = value;
				UpdateElement(StoreItemElements.Type);
			}
		}

		public StringCollection Tags {
			get {
				Load();
				return _tags;
			}
		}

		public long Id {
			get {
				return _id;
			}
			set {
				if (_id == NULL_ID || _persisting) {
					//Item wasn't previously in the database, so it's ok
					//to set a new ID
					_id = value;
				} else {
					//Item already has an id.  There's no legitimate use case 
					//in which the item's ID is changed
					throw new InvalidOperationException("Cannot change an existing item ID");
				}
			}
		}

		ICatalogStore ICatalogStoreItem.Store {
			get {
				return this.Store;
			}
		}

		ICatalogStoreItem ICatalogStoreItem.Parent {
			get {
				Load();
				return _parent;
			}
			set {
				Load();
				this.Parent = (SqliteCatalogStoreItem)value;
			}
		}

		ICatalogStoreItem ICatalogStoreItem.AliasOf {
			get {
				Load();
				return _aliasOf;
			}
			set {
				Load();
				this.AliasOf = (SqliteCatalogStoreItem)value;
			}
		}

		ICatalogStoreItemCollection ICatalogStoreItem.Children {
			get {
				Load();
				return _children;
			}
		}

		ICatalogStoreItemCollection ICatalogStoreItem.Aliases {
			get {
				Load();
				return _aliases;
			}
		}
		#endregion
		
		public SqliteCatalogStore Store {
			get {
				Load();
				return _store;
			}
		}

		public SqliteCatalogStoreItem Parent {
			get {
				Load();
				return _parent;
			}
			set {
				Load();
				_parent = value;
				UpdateElement(StoreItemElements.Parent);
			}
		}

		public SqliteCatalogStoreItem AliasOf {
			get {
				Load();
				return _aliasOf;
			}
			set {
				if (!_persisting) {
					//AliasOf is changing.  Remove this item 
					//from the alias list of the old value, and add it
					//to the new
					//
					//Note that, unfortunately, setting AliasOf to the item
					//it's already set to is used by ItemPersistor.AddItem
					//when the AliasOf item is added, and thus assigned an ID that
					//needs to be reflected in the alias_catalog_item_id column
					//for this item.  Therefore, do not attempt to optimize
					//by detecting when _aliasOf and value are the same and doing
					//nothing as a result.
					Load();
					if (_aliasOf != null && _aliasOf != value) {
						_aliasOf.Aliases.Remove(this);
					}

					if (value != null && _aliasOf != value) {
						value.Aliases.Add(this);
					}

					_aliasOf = value;

					UpdateElement(StoreItemElements.AliasOf);
				} else {
					_aliasOf = value;
				}
			}
		}
		
		public SqliteCatalogStoreItemCollection Children {
			get {
				Load();
				return _children;
			}
		}

		public SqliteCatalogStoreItemCollection Aliases {
			get {
				Load();
				return _aliases;
			}
		}

        /// <summary>Loads the item's details in response to an access of a property backed by
        ///     the data store.</summary>
		private void Load() {
			if (!_loaded && Stored && !_persisting) {
				if (!_store.Persistor.GetItem(this)) {
					throw new ApplicationException(String.Format("No item having ID {0} was found in the store {1}", 
																 _id, 
																 _store.Uri));
				}

				_loaded = true;
			}
		}

        /// <summary>Updates the database to reflect a change to one of the item's elements.</summary>
        /// 
        /// <param name="elements"></param>
		private void UpdateElement(StoreItemElements elements) {
			if (_loaded && Stored && !_persisting) {
				//Item is already in the database, so update it
				_store.Persistor.UpdateItem(this, elements);
			}
		}
	}
}
