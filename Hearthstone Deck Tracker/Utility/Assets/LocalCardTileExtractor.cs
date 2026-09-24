using Hearthstone_Deck_Tracker.Utility.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Hearthstone_Deck_Tracker.Utility.Assets
{
	/// <summary>
	/// Batches missing deck tiles and extracts them from the installed game's Unity bundles.
	/// The helper is optional: an unavailable Python/UnityPy installation leaves placeholders.
	/// </summary>
	internal static class LocalCardTileExtractor
	{
		private static readonly object Sync = new();
		private static readonly Dictionary<string, TaskCompletionSource<bool>> Requests = new();
		private static readonly HashSet<string> Pending = new();
		private static readonly HashSet<string> Unavailable = new();
		private static bool _workerRunning;
		private static readonly Regex CardIdPattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

		private static string TileDirectory => Path.Combine(Config.AppDataPath, "Images", "CardTiles");

		public static Task<bool> EnsureTileAsync(string cardId)
		{
			if(string.IsNullOrWhiteSpace(cardId) || !CardIdPattern.IsMatch(cardId))
				return Task.FromResult(false);
			if(File.Exists(Path.Combine(TileDirectory, cardId + ".jpg")))
				return Task.FromResult(true);

			lock(Sync)
			{
				if(Unavailable.Contains(cardId))
					return Task.FromResult(false);
				if(!Requests.TryGetValue(cardId, out var completion))
				{
					completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
					Requests.Add(cardId, completion);
					Pending.Add(cardId);
				}
				if(!_workerRunning)
				{
					_workerRunning = true;
					_ = Task.Run(ProcessQueueAsync);
				}
				return completion.Task;
			}
		}

		private static async Task ProcessQueueAsync()
		{
			await Task.Delay(150).ConfigureAwait(false); // Gather the cards in one deck render.
			while(true)
			{
				string[] batch;
				lock(Sync)
				{
					if(Pending.Count == 0)
					{
						_workerRunning = false;
						return;
					}
					batch = Pending.ToArray();
					Pending.Clear();
				}

				try
				{
					await Task.Run(() => RunExtractor(batch)).ConfigureAwait(false);
				}
				catch(Exception ex)
				{
					Log.Error($"Local card art extraction failed: {ex.Message}");
				}
				foreach(var cardId in batch)
				{
					var exists = File.Exists(Path.Combine(TileDirectory, cardId + ".jpg"));
					TaskCompletionSource<bool> completion;
					lock(Sync)
					{
						completion = Requests[cardId];
						Requests.Remove(cardId);
						if(!exists)
							Unavailable.Add(cardId);
					}
					completion.TrySetResult(exists);
				}
			}
		}

		private static void RunExtractor(string[] cardIds)
		{
			var gameDir = Config.Instance.HearthstoneDirectory;
			var script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Utility", "Assets", "extract_card_tiles.py");
			if(!File.Exists(Path.Combine(gameDir, "Data", "Win", "asset_manifest.unity3d")) || !File.Exists(script))
				return;

			Directory.CreateDirectory(TileDirectory);
			var arguments = $"{Quote(script)} --game-dir {Quote(gameDir)} --output {Quote(TileDirectory)} "
				+ string.Join(" ", cardIds); // EnsureTileAsync permits only safe card-ID characters.
			var start = new ProcessStartInfo("python", arguments)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			using(var process = Process.Start(start))
			{
				if(process == null)
					return;
				var output = process.StandardOutput.ReadToEndAsync();
				var error = process.StandardError.ReadToEndAsync();
				if(!process.WaitForExit(180_000))
				{
					process.Kill();
					Log.Warn("Local card art extraction timed out");
					return;
				}
				if(process.ExitCode != 0)
					Log.Warn($"Local card art extraction exited with code {process.ExitCode}: {error.GetAwaiter().GetResult()}");
				else
					Log.Info(output.GetAwaiter().GetResult().Trim());
			}
		}

		private static string Quote(string path) => "\"" + path.Replace("\"", "\\\"") + "\"";
	}
}
