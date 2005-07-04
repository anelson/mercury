using System;

namespace Mercury.Core.CatalogStore
{
    /// <summary>Interface provided to a catalog store to provide title tokenization services.
    ///     A title tokenizer breaks a title down into tokens (words in most cases), which
    ///     are then stored individually for faster searching.  The title tokenizer also
    ///     cannonicalizes search terms using related logic.</summary>
	public interface ITitleTokenizer
	{
		String[] TokenizeTitle(String title);
		String[] CannonicalizeSearchTerm(String searchTerm);
	}
}
