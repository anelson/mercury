using System;
using System.Collections;
using System.Text.RegularExpressions;

namespace Mercury.Core.CatalogStore
{
    /// <summary>Standard implementation of ITitleTokenizer, which tokenizes titles into words
    ///     based on word separator chars, underscores and other punctuation, and transitions
    ///     between cases.</summary>
	public class StandardTokenizer : ITitleTokenizer
	{
		#region ITitleTokenizer Members

		public String[] TokenizeTitle(String title) {
			ArrayList alWords = new ArrayList();

			//Use a regex to pick out the words
			//Use the unicode categories from http://www.unicode.org/Public/UNIDATA/UCD.html#General_Category_Values
			//
			//A word is a contiguous collection of characters in one of the following classes:
			// Lu/Ll/Lt/Lm/Lo - Letters
			// Nd/Nl/No - Numbers
			// Pc/Pd/Ps/Pe/Pi/Pf/Po - Punctuation
			// Sm/Sc/Sk/So - Symbols
			//
			// Within a group of letters, additional words are distinguished based on case.  The following
			// combinations are recognized as distinct words:
			//	A string of all upper-case or all lower-case letters, mixed with optional Lm/Lo characters
			//	A string with a leading upper-case character followed by lower case/Lm/Lo characters
			//  Multiple upper-case followed by Ll/Lm/Lo is interpreted as two words; all but the last
			//	 upper-case character form the first word, while the right-most upper-case character
			//	 and the following Ll/Lm/Lo characters form the second word
			//
			//All other characters are ignored for the purposes of building the list of words
			//
			//Examples:
			//  'foo bar baz' - {'foo', 'bar', 'baz'}
			//  '1234 sucks!' - {'1234', 'sucks', '!'}
			//  'more fuckin' $$$!!!' - {'more', 'fuckin', ''', '$$$', '!!!'}
			//  'fear!s the m1nd-k1ller' - {'fear', '!', 's', 'the', 'm', '1', 'nd', '-', 'k', '1', 'ller'}

			const String LETTERS_GROUP = @"[\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}]";
			const String NUMBERS_GROUP = @"[\p{Nd}\p{Nl}\p{No}]";
			const String PUNCT_GROUP = @"[\p{Pc}\p{Pd}\p{Ps}\p{Pe}\p{Pi}\p{Pf}\p{Po}]";
			const String SYM_GROUP = @"[\p{Sm}\p{Sc}\p{Sk}\p{So}]";

			Regex re = new Regex(String.Format("{0}+|{1}+|{2}+|{3}+", LETTERS_GROUP, NUMBERS_GROUP, PUNCT_GROUP, SYM_GROUP));

			MatchCollection matches = re.Matches(title);

			foreach (Match match in matches) {
				//If this is a letters match, do additional word breaking based on case
				//TODO: this could be more efficient by checking the match group the expression matched
				if (Regex.IsMatch(match.Value, LETTERS_GROUP + "+")) {
					String word = match.Value;

					//Insert '|' to denote word-breaks wherever a telltale wordbreak appears

					//First, break at every Upper-to-lower transition
					word = Regex.Replace(word, @"\p{Lu}[\p{Lm}\p{Lo}]*\p{Ll}", new MatchEvaluator(WordBreakMatchEvaluator));

					//Next, break at any Lower-to-upper transitions
					word = Regex.Replace(word, @"\p{Ll}[\p{Lm}\p{Lo}]*\p{Lu}", new MatchEvaluator(WordBreakMatchEvaluator));

					//Now, split the 'word' into the component words, and add them to the word list
					String[] words = word.Split('|');
					foreach (String splitWord in words) {
						//There will be empty strings when the word break is placed at the beginning of 
						//the string; don't include those obviously
						if (splitWord != String.Empty) {
							alWords.Add(splitWord.ToLower());
						}
					}
				} else {
					alWords.Add(match.Value.ToLower());
				}
			}

			return (String[])alWords.ToArray(typeof(String));
		}

		public String[] CannonicalizeSearchTerm(String searchTerm) {
			//Apply the same splitting logic as applied to titles.
			//In the majority of cases, search strings will be
			//single-word lower-case strings like cpal or td or qxp, 
			//however advanced users might refine the search by putting
			//spaces between letters that stand for different words, like
			//t d or CPal.
			return TokenizeTitle(searchTerm);
		}

		private String WordBreakMatchEvaluator(Match match) {
			//Called when doing Regex.Replace operations in TokenizeTitle to insert word breaks
			//into the string

			//If the first letter of the match is upper case, break before the first
			//letter.  If the first letter of the match is lower case, break after
			//the first letter
			if (Regex.IsMatch(match.Value, @"^\p{Ll}")) {
				//This is a lower-to-upper transition, so insert the break after the
				//lower case char
				//TODO: Is this assumption that the lower-case char is one
				//char long valid for all unicode glyphs?
				return match.Value.Substring(0, 1) + "|" + match.Value.Substring(1);
			} else {
				return "|" + match.Value;
			}
		}

		#endregion
	}
}
