using MongoDB.Driver;
using System;
using System.Reflection;

namespace TwitchBot.Database {
	public static class Mongo {
		public static readonly string StreamsCollectionName = "Streams";

		public static readonly IMongoDatabase Database;

		static Mongo() {

			// Ensure the database configuration properties exist
			if ( string.IsNullOrEmpty( Config.DatabaseName ) ||
				string.IsNullOrEmpty( Config.DatabaseServerAddress ) ||
				Config.DatabaseServerPort < 0 || Config.DatabaseServerPort > 65536 ||
				string.IsNullOrEmpty( Config.DatabaseUserName ) ||
				string.IsNullOrEmpty( Config.DatabaseUserPassword )
			) {
				Log.Error( "One or more of the database configuration properties are not set!" );
				Environment.Exit( 1 );
				return;
			}

			// Construct the connection URL
			MongoUrl connectionUrl = new MongoUrlBuilder() {
				Server = new MongoServerAddress( Config.DatabaseServerAddress, Config.DatabaseServerPort ),
				Username = Config.DatabaseUserName,
				Password = Config.DatabaseUserPassword,
				DatabaseName = Config.DatabaseName,
				ApplicationName = Assembly.GetExecutingAssembly().GetName().Name,
				DirectConnection = true
			}.ToMongoUrl();

			// Create the MongoDB client
			MongoClient mongoClient = new( connectionUrl );
			Log.Info( $"Initialized MongoDB client (Server: '{connectionUrl.Server}', User: '{connectionUrl.Username}')." );

			// Get the database
			Database = mongoClient.GetDatabase( connectionUrl.DatabaseName );
			Log.Info( $"Got Mongo database '{connectionUrl.DatabaseName}'." );

		}
	}
}
