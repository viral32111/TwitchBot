using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace TwitchBot {
	public static class Database {

		// The connection to the database
		private static readonly MySqlConnection databaseConnection;

		// Runs on initialization...
		static Database() {

			// Check the server connection details & user credentials are set
			if ( string.IsNullOrEmpty( Config.DatabaseName ) || string.IsNullOrEmpty( Config.DatabaseServerAddress ) || Config.DatabaseServerPort < 0 || Config.DatabaseServerPort > 65536 || string.IsNullOrEmpty( Config.DatabaseUserName ) || string.IsNullOrEmpty( Config.DatabaseUserPassword ) ) {
				Log.Error( "One or more of the database connection details and/or database user credentials configuration properties are not set!" );
				Environment.Exit( 1 );
				return;
			}

			// Create the connection using the configured connection details & user credentials
			databaseConnection = new( new MySqlConnectionStringBuilder() {
				Database = Config.DatabaseName,
				Server = Config.DatabaseServerAddress,
				Port = ( uint ) Config.DatabaseServerPort,
				UserID = Config.DatabaseUserName,
				Password = Config.DatabaseUserPassword,
				SslMode = MySqlSslMode.Disabled
			}.GetConnectionString( true ) );
			Log.Info( "Initialized the database connection." );

		}

		// Opens the connection to the database
		public static async Task Open() {
			if ( databaseConnection.State == ConnectionState.Open ) throw new Exception( "Database connection already open" );
			await databaseConnection.OpenAsync();
		}

		// Closes the connection to the database
		public static async Task Close() {
			if ( databaseConnection.State != ConnectionState.Open ) throw new Exception( "Database connection not yet opened" );
			await databaseConnection.CloseAsync();
		}

		// Gets the server's version string
		public static string GetServerVersion() {
			if ( databaseConnection.State != ConnectionState.Open ) throw new Exception( "Database connection not yet opened" );
			return databaseConnection.ServerVersion;
		}

		// Creates the tables in the database if they do not exist
		public static async Task SetupTables() {

			// Stream history for streak feature
			await Query( "CREATE TABLE IF NOT EXISTS StreamHistory ( " +
				"Identifier INT UNSIGNED PRIMARY KEY, " +
				"Channel INT UNSIGNED NOT NULL, " +
				"Start DATETIME NOT NULL, " +
				"Duration INT UNSIGNED NOT NULL COMMENT 'Seconds'" +
			" );" );

		}

		// Queries the database
		public static async Task Query( string sql ) {
			if ( databaseConnection.State != ConnectionState.Open ) throw new Exception( "Database connection not yet opened" );

			MySqlCommand command = new( sql, databaseConnection );
			int rowsAffected = await command.ExecuteNonQueryAsync();
			//Log.Debug( "Ran database query: '{0}' (rows affected: {1})", command.CommandText, rowsAffected );
		}

		public static async Task<T?> QueryWithResult<T>( string sql ) {
			if ( databaseConnection.State != ConnectionState.Open ) throw new Exception( "Database connection not yet opened" );

			MySqlCommand command = new( sql, databaseConnection );
			object? result = await command.ExecuteScalarAsync();
			//Log.Debug( "Ran database query: '{0}' (result: '{1}')", command.CommandText, result );

			return ( T? ) result;
		}

		public static async Task<DbDataReader> QueryWithResults( string sql ) {
			if ( databaseConnection.State != ConnectionState.Open ) throw new Exception( "Database connection not yet opened" );

			MySqlCommand command = new( sql, databaseConnection );
			DbDataReader reader = await command.ExecuteReaderAsync();
			//Log.Debug( "Ran database query: '{0}' (records affected: {1}, has rows: {2})", command.CommandText, reader.RecordsAffected, reader.HasRows );

			return reader;
		}

	}
}
