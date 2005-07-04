using System;

namespace Mercury.Core.CatalogStore
{
	/// <summary>
	/// Summary description for ICatalogManager.
	/// </summary>
	public interface ICatalogStoreManager {
		ITitleTokenizer Tokenizer {get; set;}
		ICatalogStore OpenCatalogStore(String catalogPath);
		ICatalogStore CreateCatalogStore(String catalogPath, bool overwrite);		
		void DeleteCatalogStore(String catalogPath);
	}
}
