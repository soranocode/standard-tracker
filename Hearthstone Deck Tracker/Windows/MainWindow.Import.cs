#region

using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Hearthstone_Deck_Tracker.Importing;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;
using MahApps.Metro.Controls.Dialogs;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Enums.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;
using Deck = Hearthstone_Deck_Tracker.Hearthstone.Deck;

#endregion

namespace Hearthstone_Deck_Tracker.Windows
{
	public partial class MainWindow
	{
		private bool _trackerDataImportInProgress;

		internal async void ImportTrackerData(bool firestone)
		{
			if(_trackerDataImportInProgress) return;
			_trackerDataImportInProgress = true;
			ProgressDialogController? progress = null;
			try
			{
				if(Core.Game.IsRunning && !Core.Game.IsInMenu)
				{
					await this.ShowMessageAsync("Перенос данных", "Завершите текущий матч, затем повторите перенос.");
					return;
				}
				var source = firestone ? "Firestone" : "Hearthstone Deck Tracker";
				var instructions = firestone
					? "Выберите JSON с полной историей матчей или FirestoneDB.sqlite из версии Electron.\n\n"
						+ "Для Overwolf сначала используйте «Выгрузить историю Firestone» в меню импорта. Старый user-match-history.json может содержать только часть истории.\n\n"
						+ "Будут перенесены матчи Standard и Wild. Колоды восстановятся из кодов в истории; матчи без кода останутся в общей статистике."
					: "Закройте Hearthstone Deck Tracker и выберите PlayerDecks.xml в его папке данных (обычно %APPDATA%\\HearthstoneDeckTracker).\n\n"
						+ "DeckStats.xml и DefaultDeckStats.xml будут прочитаны из той же папки. Перенесём колоды, их версии и матчи Standard и Wild.";
				if(await this.ShowMessageAsync("Перенос из " + source, instructions, MessageDialogStyle.AffirmativeAndNegative,
					new MetroDialogSettings { AffirmativeButtonText = "Выбрать файл", NegativeButtonText = "Отмена", DefaultButtonFocus = MessageDialogResult.Negative }) != MessageDialogResult.Affirmative)
					return;
				var legacyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HearthstoneDeckTracker");
				var dialog = new Microsoft.Win32.OpenFileDialog
				{
					Title = "Данные " + source,
					Filter = firestone ? "История Firestone|*.json;*.sqlite;*.sqlite3;*.db" : "Данные HDT|PlayerDecks.xml;DeckStats.xml;DefaultDeckStats.xml",
					InitialDirectory = !firestone && Directory.Exists(legacyPath) ? legacyPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
				};
				if(dialog.ShowDialog(this) != true) return;
				if(!firestone && string.Equals(Path.GetFullPath(Path.GetDirectoryName(dialog.FileName)!), Path.GetFullPath(Config.Instance.DataDir).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
					throw new InvalidDataException("Выбрана папка нашего трекера. Выберите данные Hearthstone Deck Tracker.");
				progress = await this.ShowProgressAsync("Перенос из " + source, "Читаем колоды и историю матчей…");
				progress.SetIndeterminate();
				var data = await Task.Run(() => firestone ? TrackerDataImporter.ReadFirestone(dialog.FileName) : TrackerDataImporter.ReadHdt(Path.GetDirectoryName(dialog.FileName)!));
				var preview = TrackerDataImporter.Prepare(data, DeckList.Instance, DeckStatsList.Instance, DefaultDeckStats.Instance);
				await progress.CloseAsync();
				progress = null;
				var summary = preview.Summary;
				if(preview.Warnings.Count > 0) summary += "\n\n" + string.Join("\n", preview.Warnings.Take(5));
				if(!preview.HasChanges)
				{
					await this.ShowMessageAsync("Нет новых данных для переноса", summary);
					return;
				}
				if(await this.ShowMessageAsync("Проверка переноса из " + source,
					summary + "\n\nПеред сохранением создадим резервную копию наших данных.", MessageDialogStyle.AffirmativeAndNegative,
					new MetroDialogSettings { AffirmativeButtonText = "Перенести", NegativeButtonText = "Отмена", DefaultButtonFocus = MessageDialogResult.Negative }) != MessageDialogResult.Affirmative)
					return;
				if(Core.Game.IsRunning && !Core.Game.IsInMenu)
				{
					await this.ShowMessageAsync("Перенос отложен", "Начался матч. Завершите его и повторите перенос.");
					return;
				}
				progress = await this.ShowProgressAsync("Перенос из " + source, "Сохраняем данные и резервную копию…");
				progress.SetIndeterminate();
				if(Core.Game.IsRunning && !Core.Game.IsInMenu)
				{
					await progress.CloseAsync();
					progress = null;
					await this.ShowMessageAsync("Перенос отложен", "Начался матч. Завершите его и повторите перенос.");
					return;
				}
				// Refresh and commit on the dispatcher without yielding between them. A game
				// finishing during source reading must not be overwritten by an older snapshot.
				preview = TrackerDataImporter.Prepare(data, DeckList.Instance, DeckStatsList.Instance, DefaultDeckStats.Instance);
				var backup = TrackerDataImporter.Save(preview, Config.Instance.DataDir);
				TrackerDataImporter.Apply(preview, DeckList.Instance, DeckStatsList.Instance, DefaultDeckStats.Instance);
				DeckPickerList.UpdateDecks();
				StatsOverview.UpdateStats();
				await progress.CloseAsync();
				progress = null;
				await this.ShowMessageAsync("Данные перенесены", preview.Summary + "\n\nРезервная копия: " + backup);
			}
			catch(Exception ex)
			{
				if(progress != null) { await progress.CloseAsync(); progress = null; }
				Log.Error(ex);
				await this.ShowMessageAsync("Не удалось перенести данные", ex.Message + "\n\nПроверьте выбранный файл и повторите перенос.");
			}
			finally { _trackerDataImportInProgress = false; }
		}

		internal async void ShowFirestoneExportHelp()
		{
			var result = await this.ShowMessageAsync("Выгрузить историю Firestone",
				"В версии Overwolf откройте инструменты разработчика окна Firestone и вкладку Console.\n\n"
				+ "Нажмите «Скопировать скрипт», вставьте его в консоль Firestone и выполните. Он прочитает всю таблицу матчей и скопирует JSON в буфер обмена.\n\n"
				+ "Вставьте результат в Блокнот, сохраните как firestone-history.json в UTF-8 и выберите этот файл при переносе.\n\n"
				+ "Для версии Electron можно сразу выбрать FirestoneDB.sqlite. Закройте Firestone перед чтением базы.",
				MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings { AffirmativeButtonText = "Скопировать скрипт", NegativeButtonText = "Закрыть" });
			if(result != MessageDialogResult.Affirmative) return;
			try
			{
				using(var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("FirestoneExport.js"))
				using(var reader = new StreamReader(stream!))
					System.Windows.Clipboard.SetText(reader.ReadToEnd());
			}
			catch(Exception ex)
			{
				Log.Error(ex);
				await this.ShowMessageAsync("Скрипт не скопирован", "Не удалось записать скрипт в буфер обмена. Повторите копирование.");
			}
		}

		public async void ImportDeck(string? url = null)
		{
			var result = await ImportDeckFromUrl(url);
			if(result.WasCancelled)
				return;
			if(result.Deck != null)
				await ShowImportingChoice(result.Deck);
			else
				await this.ShowMessageAsync("No deck found", "Could not find a deck on" + Environment.NewLine + result.Url);
		}

		public class ImportingResult
		{
			public Deck? Deck { get; set; }
			public string? Url { get; set; }
			public bool WasCancelled { get; set; }
		}

		private async Task<ImportingResult> ImportDeckFromUrl(string? url = null, bool checkClipboard = true)
		{
			var fromClipboard = false;
			if(url == null)
			{
				if(checkClipboard)
				{
					try
					{
						var clipboard = Clipboard.ContainsText() ? new string(Clipboard.GetText().Take(1000).ToArray()) : "";
						if(Helper.IsValidUrl(clipboard))
						{
							url = clipboard;
							fromClipboard = true;
						}
					}
					catch(Exception e)
					{
						Log.Error(e);
					}
				}
				if(url == null)
					url = await InputDeckUrl();
			}
			if(url == null)
				return new ImportingResult { WasCancelled = true };
			var controller = await this.ShowProgressAsync("Loading Deck", "Please wait...");
			var deck = await DeckImporter.Import(url);
			if(deck != null && string.IsNullOrEmpty(deck.Url))
				deck.Url = url;
			await controller.CloseAsync();
			if(deck == null && fromClipboard)
				return await ImportDeckFromUrl(checkClipboard: false);
			return new ImportingResult { Deck = deck, Url = url };
		}

		private async Task<string?> InputDeckUrl()
		{
			try
			{
				return await this.ShowWebImportingDialog();
			}
			catch(Exception e)
			{
				Log.Error(e);
				return null;
			}
		}

		internal void ImportFromLastGame()
		{
			if(Core.Game.DrawnLastGame == null)
				return;
			var deck = new Deck();
			foreach(var card in Core.Game.DrawnLastGame)
			{
				if(card.IsCreated)
					continue;

				deck.Cards.Add(card);

				if(string.IsNullOrEmpty(deck.Class) && card.GetPlayerClass != "Neutral")
					deck.Class = card.PlayerClass;
			}

			ShowDeckEditorFlyout(deck, true);
		}

		internal async void ShowImportDialog(bool brawl)
		{
			DeckImportingFlyout.Reset(brawl);
			FlyoutDeckImporting.IsOpen = true;
			if(!Core.Game.IsRunning)
			{
				Log.Info("Waiting for game...");
				while(!Core.Game.IsRunning)
					await Task.Delay(500);
			}
			DeckImportingFlyout.StartedGame();
			var mode = brawl ? Mode.TAVERN_BRAWL : Mode.TOURNAMENT;
			if(Core.Game.CurrentMode != mode)
			{
				Log.Info($"Waiting for {mode} screen...");
				while(Core.Game.CurrentMode != mode)
					await Task.Delay(500);
			}
			var decks = brawl ? DeckImporter.FromBrawl() : DeckImporter.FromConstructed();
			DeckImportingFlyout.SetDecks(decks);
			ActivateWindow();
		}

		private bool _clipboardImportingInProgress;
		internal async void ImportFromClipboard()
		{
			if(_clipboardImportingInProgress)
				return;
			_clipboardImportingInProgress = true;
			var deck = await ClipboardImporter.Import();
			if(deck == null)
			{
				const string dialogTitle = "MainWindow_Import_Dialog_NoDeckFound_Title";
				const string dialogText = "MainWindow_Import_Dialog_NoDeckFound_Text";
				this.ShowMessage(LocUtil.Get(dialogTitle), LocUtil.Get(dialogText)).Forget();
				_clipboardImportingInProgress = false;
				return;
			}
			await ShowImportingChoice(deck);
			_clipboardImportingInProgress = false;
		}

		private async Task ShowImportingChoice(Deck deck)
		{
			var choice = Config.Instance.PasteImportingChoice == ImportingChoice.Manual
				? await this.ShowImportingChoiceDialog() : Config.Instance.PasteImportingChoice;
			if(choice.HasValue)
			{
				if(choice.Value == ImportingChoice.SaveLocal)
					ShowDeckEditorFlyout(deck, true);
				else
					ShowExportFlyout(deck);
			}
		}
	}
}
