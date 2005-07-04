using System;
using System.Collections;

using Finisar.SQLite;

using Mercury.Core.CatalogStore;

namespace Mercury.Core.CatalogStore.Sqlite
{
	/// <summary>
	/// Summary description for SqliteCatalogStoreItemCollection.
	/// </summary>
	public class SqliteCatalogStoreItemCollection : CollectionBase, ICatalogStoreItemCollection
	{
		public SqliteCatalogStoreItemCollection()
		{
			//
			// TODO: Add constructor logic here
			//
		}

		public ICatalogStoreItem this[ int index ]  {
			get  {
				return( (ICatalogStoreItem) List[index] );
			}
		}

		public int IndexOf( ICatalogStoreItem value ) {
			return( List.IndexOf( value ) );
		}

		public bool Contains( ICatalogStoreItem value ) {
			// If value is not of type ICatalogStoreItem, this will return false.
			return( List.Contains( value ) );
		}

		internal int Add( SqliteCatalogStoreItem value ) {
			return Add((ICatalogStoreItem)value);
		}

		internal int Add( ICatalogStoreItem value ) {
			return( List.Add( value ) );
		}

		public int IndexOf( SqliteCatalogStoreItem value ) {
			return IndexOf((ICatalogStoreItem)value);
		}

		internal void Insert( int index, SqliteCatalogStoreItem value ) {
			Insert(index, (ICatalogStoreItem)value);
		}

		internal void Insert( int index, ICatalogStoreItem value ) {
			List.Insert( index, value );
		}

		internal void Remove( SqliteCatalogStoreItem value ) {
			Remove((ICatalogStoreItem)value);
		}

		internal void Remove( ICatalogStoreItem value ) {
			List.Remove( value );
		}

		internal bool Contains( SqliteCatalogStoreItem value ) {
			return Contains((ICatalogStoreItem)value);   
		}

		protected override void OnInsert( int index, Object value ) {
			if ( value.GetType() != typeof(SqliteCatalogStoreItem) )
				throw new ArgumentException( "value must be of type SqliteCatalogStoreItem.", "value" );
		}

		protected override void OnRemove( int index, Object value ) {
			if ( value.GetType() != typeof(SqliteCatalogStoreItem) )
				throw new ArgumentException( "value must be of type SqliteCatalogStoreItem.", "value" );
		}

		protected override void OnSet( int index, Object oldValue, Object newValue ) {
			if ( newValue.GetType() != typeof(SqliteCatalogStoreItem) )
				throw new ArgumentException( "newValue must be of type SqliteCatalogStoreItem.", "newValue" );
		}

		protected override void OnValidate( Object value ) {
			if ( value.GetType() != typeof(SqliteCatalogStoreItem) )
				throw new ArgumentException( "value must be of type SqliteCatalogStoreItem." );
		}
	}
}
