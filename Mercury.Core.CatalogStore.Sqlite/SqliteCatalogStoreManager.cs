using System;
using System.Data;
using System.IO;
using System.Reflection;

using Finisar.SQLite;

using Mercury.Core.CatalogStore;

namespace Mercury.Core.CatalogStore.Sqlite
{
	public class SqliteCatalogStoreManager : ICatalogStoreManager {
		ITitleTokenizer _tokenizer = null;
		
		public SqliteCatalogStoreManager() {
			//Use the standard tokenizer
			_tokenizer = new StandardTokenizer();
		}
		
#region ICatalogStoreManager Members

		public ITitleTokenizer Tokenizer {
			get {
				return _tokenizer;
			}

			set {
				_tokenizer = value;
			}
		}
		
		ICatalogStore ICatalogStoreManager.OpenCatalogStore(String catalogPath) {
			return this.OpenCatalogStore(catalogPath);
		}
		
        /// <summary>Creates a new catalog store, optionally overwriting an existing one</summary>
        /// 
        /// <param name="catalogPath">Path and file name of store to create</param>
        /// <param name="overwrite">True to overwrite an existing store.  If false, then catalogPath must not exist;
        ///     if it does, an exception is thrown.</param>
        /// 
        /// <returns>The newly created catalog store</returns>
		ICatalogStore ICatalogStoreManager.CreateCatalogStore(String catalogPath, bool overwrite) {
			return this.CreateCatalogStore(catalogPath, overwrite);
		}
		
        /// <summary>Deletes a catalog store.  If the store doesn't exist or can't be deleted,
        ///     throws an exception</summary>
		public void DeleteCatalogStore(String catalogPath) {
			if (!File.Exists(catalogPath)) {
				throw new FileNotFoundException("Catalog file not found",  catalogPath);
			}

			File.Delete(catalogPath);
		}
		
#endregion
		
		public SqliteCatalogStore OpenCatalogStore(String catalogPath) {
			//Make sure the catalog exists
			if (!File.Exists(catalogPath)) {
				throw new FileNotFoundException("Catalog file not found",  catalogPath);
			}
			
			//Connect to the catalog
			SQLiteConnection conn = SqliteCatalogStoreManager.OpenConnection(catalogPath,  false);

			SqliteCatalogStore store = new SqliteCatalogStore(this, conn, catalogPath);

			return store;
		}
		
        /// <summary>Creates a new catalog store, optionally overwriting an existing one</summary>
        /// 
        /// <param name="catalogPath">Path and file name of store to create</param>
        /// <param name="overwrite">True to overwrite an existing store.  If false, then catalogPath must not exist;
        ///     if it does, an exception is thrown.</param>
        /// 
        /// <returns>The newly created catalog store</returns>
		public SqliteCatalogStore CreateCatalogStore(String catalogPath, bool overwrite) {
			//Ensure the path doesn't already exist unless overwrite
			//is specified
			if (File.Exists(catalogPath)) {
				if (overwrite) {
					DeleteCatalogStore(catalogPath);
				} else {
					//File already exists
					throw new ArgumentException("The specified catalog path already exists",  "catalogPath");
				}
			} 

			//Create the database and build the schema
			using (SQLiteConnection conn = SqliteCatalogStoreManager.OpenConnection(catalogPath, true)) {
				//The database init script is stored an an embedded resource
				//within this assembly.  Load it and run it like any other command.			
				TextReader tr = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Mercury.Core.CatalogStore.Sqlite.MercDb.sql"));
				using (SQLiteCommand cmd = conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					cmd.CommandText = tr.ReadToEnd();
					tr.Close();

					cmd.ExecuteNonQuery();
				}
				conn.Close();
			}

			//Now the catalog has been created, and is simply empty.  Use
			//the open method
			return OpenCatalogStore(catalogPath);
		}

        /// <summary>Connects to a SQLite database</summary>
        /// 
        /// <param name="dbPath">Path of data file</param>
        /// <param name="createNew">True to create a new data file.</param>
        /// 
        /// <returns></returns>
		private static SQLiteConnection OpenConnection(String dbPath, bool createNew) {
			SQLiteConnection conn = new SQLiteConnection();
			conn.ConnectionString = BuildConnectString(dbPath, createNew);
			conn.Open();

			return conn;
		}

        /// <summary>Composes and returns a SQLite connect string</summary>
        /// 
        /// <param name="dbPath">Path of database to connect to </param>
        /// <param name="createNew">true if a new database is to be created; else false</param>
        /// 
        /// <returns></returns>
		private static String BuildConnectString(String dbPath, bool createNew) {
			String connStr = String.Format("Data Source={0};Compress=False;UTF8Encoding=True;Version=3;Cache Size=10000",
										   dbPath);
			if (createNew) {
				connStr += ";New=True";
			}

			return connStr;
		}
	}
}
