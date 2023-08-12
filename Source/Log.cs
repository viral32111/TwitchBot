using System;
using System.Diagnostics;

namespace TwitchBot;

public static class Log {
	private readonly static ConsoleColor originalConsoleColor = Console.ForegroundColor;

	public static void Write( string severity, string format, params object?[] objects ) {
		Console.WriteLine( "[{0:dd-MM-yyyy HH:mm:ss.fff zzz}] [{1}] {2}", DateTime.UtcNow, severity.ToUpper(), string.Format( format, objects ) );
	}

	[Conditional( "DEBUG" )]
	public static void Debug( string format, params object?[] objects ) {
		Console.ForegroundColor = ConsoleColor.Cyan;
		Write( "DEBUG", format, objects );
		Console.ForegroundColor = originalConsoleColor;
	}

	public static void Info( string format, params object?[] objects ) {
		Console.ForegroundColor = ConsoleColor.White;
		Write( "INFO", format, objects );
		Console.ForegroundColor = originalConsoleColor;
	}

	public static void Warn( string format, params object?[] objects ) {
		Console.ForegroundColor = ConsoleColor.Yellow;
		Write( "WARN", format, objects );
		Console.ForegroundColor = originalConsoleColor;
	}

	public static void Error( string format, params object?[] objects ) {
		Console.ForegroundColor = ConsoleColor.Red;
		Write( "ERROR", format, objects );
		Console.ForegroundColor = originalConsoleColor;
	}
}
