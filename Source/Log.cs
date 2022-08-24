using System;

namespace TwitchBot {
	public static class Log {
		public static void Write( string format, params object?[] objects ) {
			Console.WriteLine( "[{0:dd-MM-yyyy HH:mm:ss.fff zzz}] {1}", DateTime.UtcNow, string.Format( format, objects ) );
		}
	}
}
