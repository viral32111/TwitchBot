using System;
using System.Reflection;
using System.Threading.Tasks;

using StackExchange.Redis;

namespace TwitchBot;

public static class Redis {

	private static readonly ConfigurationOptions connectOptions;

	private static ConnectionMultiplexer? connection;
	private static IDatabase? database;

	static Redis() {
		if ( string.IsNullOrEmpty( Config.RedisServerAddress ) || Config.RedisServerPort < 0 || Config.RedisServerPort > 65536 || string.IsNullOrEmpty( Config.RedisUserName ) || string.IsNullOrEmpty( Config.RedisUserPassword ) ) {
			Log.Error( "One or more of the Redis connection details and/or Redis user credentials configuration properties are not set!" );
			Environment.Exit( 1 );
			return;
		}

		connectOptions = ConfigurationOptions.Parse( $"{Config.RedisServerAddress}:{Config.RedisServerPort}" );
		connectOptions.ClientName = Assembly.GetExecutingAssembly().GetName().Name;
		connectOptions.User = Config.RedisUserName;
		connectOptions.Password = Config.RedisUserPassword;
		connectOptions.AbortOnConnectFail = false;
		Log.Info( "Initialized Redis connection options." );
	}

	public static async Task Open() {
		if ( database != null && connection != null && connection.IsConnected ) throw new Exception( "Redis connection already opened" );

		connection = await ConnectionMultiplexer.ConnectAsync( connectOptions );
		database = connection.GetDatabase( Config.RedisDatabase );
	}

	public static async Task Close() {
		if ( database == null || connection == null || !connection.IsConnected ) throw new Exception( "Redis connection not yet opened" );
		await connection.CloseAsync();
	}

	public static async Task Set( string key, string value ) {
		if ( database == null || connection == null || !connection.IsConnected ) throw new Exception( "Redis connection not yet opened" );
		await database.StringSetAsync( Config.RedisKeyPrefix + key, value );
	}

	public static async Task<string?> Get( string key ) {
		if ( database == null || connection == null || !connection.IsConnected ) throw new Exception( "Redis connection not yet opened" );
		return await database.StringGetAsync( Config.RedisKeyPrefix + key );
	}

}
