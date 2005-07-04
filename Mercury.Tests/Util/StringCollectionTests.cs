using System;
using System.Collections;

using NUnit.Framework;

using Mercury.Util;

namespace Mercury.Tests.Util
{
	[TestFixture]
	public class StringCollectionTests
	{
		[Test]
		public void EmptyBehaviorTest() {
			StringCollection col = new StringCollection();

			Assert.AreEqual(0, col.Count);
			Assert.IsFalse(col.Contains(String.Empty));
			Assert.AreEqual(-1, col.IndexOf(String.Empty));
		}

		[Test]
		public void AddInsertTest() {
			StringCollection col = new StringCollection();

			String test = "foo";
			Assert.IsFalse(col.Contains(test));
			Assert.AreEqual(-1, col.IndexOf(test));

			col.Add(test);
			Assert.IsTrue(col.Contains(test));
			Assert.AreEqual(0, col.IndexOf(test));

			String test2 = "bar";

			col.Insert(0, test2);
			Assert.IsTrue(col.Contains(test2));
			Assert.AreEqual(0, col.IndexOf(test2));
			Assert.IsTrue(col.Contains(test));
			Assert.AreEqual(1, col.IndexOf(test));
		}

		[Test]
		public void RemoveTest() {
			StringCollection col = new StringCollection();

			String test = "foo", test2 = "bar";
			col.Add(test);
			col.Add(test2);

			col.RemoveAt(1);

			Assert.IsTrue(col.Contains(test));
			Assert.IsFalse(col.Contains(test2));
			
			col.Remove(test);

			Assert.IsFalse(col.Contains(test));
		}

		[Test]
		[ExpectedException(typeof(ArgumentException))]
		public void RemoveNonExistentTest() {
			StringCollection col = new StringCollection();

			String test = "foo", test2 = "bar";
			col.Add(test);
			col.Add(test2);

			col.Remove("baz");
		}

		[Test]
		public void ThisTest() {
			StringCollection col = new StringCollection();
			String test = "foo", test2 = "bar";

			col.Add(test);
			col.Add(test2);

			Assert.AreEqual(test, col[0]);
			Assert.AreEqual(test2, col[1]);

			col[1] = test;	
			Assert.AreEqual(test, col[1]);
		}
	}
}
