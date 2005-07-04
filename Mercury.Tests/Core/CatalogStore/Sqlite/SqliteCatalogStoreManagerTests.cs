using System;
using System.Collections;
using System.IO;

using NUnit.Framework;

using Mercury.Core.CatalogStore;
using Mercury.Core.CatalogStore.Sqlite;

namespace Mercury.Tests.Core.CatalogStore.Sqlite
{
	[TestFixture]
	public class SqliteCatalogStoreManagerTests
	{
		static ArrayList _storesToCleanup = new ArrayList();

		[TestFixtureTearDown]
		public void CleanupStores() {
			//Clean up all the stores created during the test
			foreach (String store in _storesToCleanup) {
				File.Delete(store);
			}
			_storesToCleanup.Clear();
		}

		[Test]
		public void CreateNewStoreTest() {
			SqliteCatalogStoreManager mgr = new SqliteCatalogStoreManager();

			String storePath = GetThrowawayStorePath();
			using (ICatalogStore store = mgr.CreateCatalogStore(storePath, false)) {
			}
			_storesToCleanup.Add(storePath);
		}

		[Test]
		public void CreateDeleteStoreTest() {
			SqliteCatalogStoreManager mgr = new SqliteCatalogStoreManager();

			String storePath = GetThrowawayStorePath();
			using (ICatalogStore store = mgr.CreateCatalogStore(storePath, false)) {
			}
			mgr.DeleteCatalogStore(storePath);
			Assert.IsFalse(File.Exists(storePath));
			_storesToCleanup.Add(storePath);
		}

		[Test]
		public void OpenExistingStoreTest() {
			SqliteCatalogStoreManager mgr = new SqliteCatalogStoreManager();

			String storePath = GetThrowawayStorePath();
			using (ICatalogStore store = mgr.CreateCatalogStore(storePath, false)) {
			}
			_storesToCleanup.Add(storePath);
			using (ICatalogStore store = mgr.OpenCatalogStore(storePath)) {
			}
		}

		[Test]
		public void CreateOverwriteStoreTest() {
			SqliteCatalogStoreManager mgr = new SqliteCatalogStoreManager();

			String storePath = GetThrowawayStorePath();
			using (ICatalogStore store = mgr.CreateCatalogStore(storePath, false)) {
			}
			_storesToCleanup.Add(storePath);
			using (ICatalogStore store = mgr.CreateCatalogStore(storePath, true)) {
			}
		}

		[Test]
		[ExpectedException(typeof(ArgumentException))]
		public void CreateOverExistingStoreTest() {
			SqliteCatalogStoreManager mgr = new SqliteCatalogStoreManager();

			String storePath = GetThrowawayStorePath();
			using (ICatalogStore store = mgr.CreateCatalogStore(storePath, false)) {
			}
			_storesToCleanup.Add(storePath);
			using (ICatalogStore store = mgr.CreateCatalogStore(storePath, false)) {
			}
		}

		[Test]
		[ExpectedException(typeof(FileNotFoundException))]
		public void OpenMissingStoreTest() {
			SqliteCatalogStoreManager mgr = new SqliteCatalogStoreManager();

			String storePath = GetThrowawayStorePath();
			using (ICatalogStore store = mgr.OpenCatalogStore(storePath)) {
			}
		}

		private String GetThrowawayStorePath() {
			String file = Path.Combine(Path.GetTempPath(), Path.GetTempFileName()) + ".db";

			//Path.GetTempFileName creates the file on disk; that skews the results
			File.Delete(file);
			return file;
		}
	}
}
