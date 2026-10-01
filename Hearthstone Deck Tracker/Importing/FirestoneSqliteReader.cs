using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Hearthstone_Deck_Tracker.Importing;

// Windows 10+ provides SQLite through winsqlite3; no Firestone binaries are loaded.
internal static class FirestoneSqliteReader
{
	public static JArray ReadHistory(string path)
	{
		if(!File.Exists(path)) throw new FileNotFoundException("База Firestone не найдена.", path);
		IntPtr database = IntPtr.Zero, statement = IntPtr.Zero;
		try
		{
			Check(sqlite3_open_v2(Utf8(Path.GetFullPath(path)), out database, 1, IntPtr.Zero), database);
			Check(sqlite3_prepare_v2(database, Utf8("SELECT data FROM matchHistory"), -1, out statement, IntPtr.Zero), database);
			var rows = new JArray();
			int result;
			while((result = sqlite3_step(statement)) == 100)
			{
				var pointer = sqlite3_column_text(statement, 0);
				var length = sqlite3_column_bytes(statement, 0);
				if(pointer == IntPtr.Zero) { rows.Add(JValue.CreateNull()); continue; }
				var bytes = new byte[length];
				Marshal.Copy(pointer, bytes, 0, length);
				try { rows.Add(JToken.Parse(Encoding.UTF8.GetString(bytes))); }
				catch(Newtonsoft.Json.JsonException) { rows.Add(JValue.CreateNull()); }
			}
			if(result != 101) Check(result, database);
			return rows;
		}
		catch(DllNotFoundException ex)
		{
			throw new InvalidDataException("Для чтения SQLite нужна Windows 10 или новее. Можно импортировать JSON-историю Firestone.", ex);
		}
		finally
		{
			if(statement != IntPtr.Zero) sqlite3_finalize(statement);
			if(database != IntPtr.Zero) sqlite3_close(database);
		}
	}

	private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text + "\0");
	private static void Check(int code, IntPtr database)
	{
		if(code != 0) throw new InvalidDataException("Не удалось прочитать базу Firestone: " + (database == IntPtr.Zero ? code.ToString() : Marshal.PtrToStringAnsi(sqlite3_errmsg(database))));
	}
	[DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2(byte[] filename, out IntPtr database, int flags, IntPtr vfs);
	[DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr database, byte[] sql, int length, out IntPtr statement, IntPtr tail);
	[DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr statement);
	[DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
	[DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_bytes(IntPtr statement, int column);
	[DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_errmsg(IntPtr database);
	[DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr statement);
	[DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr database);
}
