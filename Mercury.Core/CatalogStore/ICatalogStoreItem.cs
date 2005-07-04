using System;

using Mercury.Util;

namespace Mercury.Core.CatalogStore
{
	/// <summary>
	/// Summary description for ICatalogStoreItem.
	/// </summary>
	public interface ICatalogStoreItem
	{	
		ICatalogStore Store {get;}
		long Id {get; set;}
		String Uri {get; set;}
		ICatalogStoreItem Parent {get; set;}
		ICatalogStoreItem AliasOf {get; set;}
		String Title {get; set;}
		String Type {get; set;}
		StringCollection Tags {get;}
		ICatalogStoreItemCollection Children {get;}
		ICatalogStoreItemCollection Aliases {get;}
	}
}
