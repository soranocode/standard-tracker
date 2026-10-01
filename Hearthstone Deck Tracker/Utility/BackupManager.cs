#region

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Library;
using Hearthstone_Deck_Tracker.Importing;
using StandardTracker.Library;
using Hearthstone_Deck_Tracker.Utility.Logging;

#endregion

namespace Hearthstone_Deck_Tracker.Utility
{
	public class BackupManager
	{
		private const int MaxBackups = 7;
		private static readonly string[] Files = {LibraryStore.FileName, "config.xml", "HotKeys.xml"};

		public static void Run()
		{
			Log.Info("Running BackupManager");
			if(!Directory.Exists(Config.Instance.BackupDir))
				Directory.CreateDirectory(Config.Instance.BackupDir);
			var dirInfo = new DirectoryInfo(Config.Instance.BackupDir);
			var backupFileName = $"Backup_{DateTime.Today.ToString("ddMMyyyy")}.zip";

			if (dirInfo.GetFiles().Any(x => x.Name == backupFileName))
			{
				Log.Info("Backup for today already exists");
				return;
			}

			try
			{
				var backups = dirInfo.GetFiles("Backup_*");
				while(backups.Count() > MaxBackups)
				{
					var oldest = backups.OrderBy(x => x.CreationTime).First();
					Log.Info("Deleting old backup: " + oldest.Name);
					oldest.Delete();
					backups = dirInfo.GetFiles("Backup_*");
				}
			}
			catch(Exception ex)
			{
				Log.Error("Error deleting old backup: " + ex);
			}

			Log.Info("Creating backup for today");

			CreateBackup(backupFileName);
		}

		public static void CreateBackup(string fileName)
		{
			try
			{
				var count = 1;
				var fileInfo = new FileInfo(fileName);
				while(File.Exists(Path.Combine(Config.Instance.BackupDir, fileName)))
					fileName = $"{fileInfo.Name}_{count++}.{fileInfo.Extension}";

				var backupFilePath = Path.Combine(Config.Instance.BackupDir, fileName);
				using(var zip = ZipFile.Open(backupFilePath, ZipArchiveMode.Create))
				{
					foreach(var file in Files)
					{
						var path = Path.Combine(Config.Instance.DataDir, file);
						if(File.Exists(path))
							zip.CreateEntryFromFile(path, file);
					}
				}
			}
			catch(Exception ex)
			{
				Log.Error(ex);
			}
		}

		internal static bool Restore(FileInfo backup, bool reload, params string[] files)
		{
			try
			{
				using var archive = new ZipArchive(backup.OpenRead(), ZipArchiveMode.Read);
				Directory.CreateDirectory(Config.Instance.DataDir);
				// Check the native library before replacing any current profile data.
				if((files.Length == 0 || files.Contains(LibraryStore.FileName)) && archive.GetEntry(LibraryStore.FileName) is { } libraryEntry)
				{
					var temporary = Path.Combine(Config.Instance.DataDir, "restore-library-" + Guid.NewGuid().ToString("N") + ".json");
					try
					{
						libraryEntry.ExtractToFile(temporary);
						var library = LibraryStore.ReadFile(temporary);
						new LibraryStore(Config.Instance.DataDir).Save(library);
					}
					finally { if(File.Exists(temporary)) File.Delete(temporary); }
				}
				else if(files.Length == 0 && HdtProfileReader.FileNames.Any(name => archive.GetEntry(name) != null))
				{
					// Earlier Standard Tracker backups can be explicitly restored into
					// the native format. Only known entries are read from the archive.
					var temporary = Path.Combine(Config.Instance.DataDir, "restore-previous-" + Guid.NewGuid().ToString("N"));
					Directory.CreateDirectory(temporary);
					try
					{
						foreach(var name in HdtProfileReader.FileNames)
							archive.GetEntry(name)?.ExtractToFile(Path.Combine(temporary, name));
						var source = HdtProfileReader.ReadProfile(temporary);
						var library = LibraryRuntimeAdapter.Capture(source.Decks, source.Stats, source.Defaults, Guid.NewGuid());
						new LibraryStore(Config.Instance.DataDir).Save(library);
					}
					finally { Directory.Delete(temporary, true); }
				}
				else if(files.Contains(LibraryStore.FileName)) return false;
				if(files.Length == 0)
				{
					foreach(var file in Files.Where(x => x != LibraryStore.FileName))
						archive.GetEntry(file)?.ExtractToFile(Path.Combine(Config.Instance.DataDir, file), true);
				}
				else
				{
					foreach(var file in files.Where(x => Files.Contains(x) && x != LibraryStore.FileName))
						archive.GetEntry(file)?.ExtractToFile(Path.Combine(Config.Instance.DataDir, file), true);
				}
				if(!reload)
					return true;
				if(files.Length == 0 || files.Contains("config.xml"))
				{
					Config.Load();
					Config.Save();
				}
				if(files.Length == 0 || files.Contains(LibraryStore.FileName))
				{
					DeckList.Reload();
					DeckStatsList.Reload();
					DefaultDeckStats.Reload();
					StandardLibrarySession.Save();
					LastGames.Save();
				}
				return true;
			}
			catch(Exception ex)
			{
				Log.Error(ex);
				return false;
			}
		}

		internal static bool RestoreFromLatest(bool reload, int skip = 0, params string[] files)
		{
			if(!Directory.Exists(Config.Instance.BackupDir))
				return false;
			var dirInfo = new DirectoryInfo(Config.Instance.BackupDir);
			var latest = dirInfo.GetFiles("Backup*").OrderByDescending(x => x.CreationTimeUtc).Skip(skip).FirstOrDefault();
			return latest != null && Restore(latest, reload, files);
		}

	}
}
