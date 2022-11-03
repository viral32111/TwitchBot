using System;
using System.Diagnostics;

namespace TwitchBot {
	public static class Log {
		public static void Write( string severity, string format, params object?[] objects ) {
			Console.WriteLine( "[{0:dd-MM-yyyy HH:mm:ss.fff zzz}] [{1}] {2}", DateTime.UtcNow, severity.ToUpper(), string.Format( format, objects ) );
		}

		[Conditional( "DEBUG" )]
		public static void Debug( string format, params object?[] objects ) {
			Write( "DEBUG", format, objects );
		}

		public static void Info( string format, params object?[] objects ) {
			Write( "INFO", format, objects );
		}

		public static void Warn( string format, params object?[] objects ) {
			Write( "WARN", format, objects );
		}

		public static void Error( string format, params object?[] objects ) {
			Write( "ERROR", format, objects );
		}
	}
}
