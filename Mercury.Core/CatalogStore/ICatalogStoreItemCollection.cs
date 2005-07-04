using System;
using System.Collections;

namespace Mercury.Core.CatalogStore
{
	/// <summary>
	/// Summary description for ICatalogStoreItemCollection.
	/// </summary>
	public interface ICatalogStoreItemCollection : ICollection {
		ICatalogStoreItem this[ int index ]  {get;}
		int IndexOf( ICatalogStoreItem value );
		bool Contains( ICatalogStoreItem value );
	}
}
