using System;

namespace Mercury.Core.CatalogStore
{
	/// <summary>
	/// Summary description for ICatalog.
	/// </summary>
	public interface ICatalogStore : IDisposable
	{
		ICatalogStoreManager Manager {get;}
		String Uri {get;}

		void BeginBatch();
		void EndBatch();

		ICatalogStoreItem CreateItem();
		
		void AddItem(ICatalogStoreItem item);
		void RemoveItem(ICatalogStoreItem item);

		ICatalogStoreItem GetItem(long id);
		ICatalogStoreItem GetItem(long id, bool forceLoad);

		bool ItemExists(long id);

		ICatalogStoreItemCollection GetRootItems();
	}
}

