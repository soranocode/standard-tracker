using System;
using System.Linq;
using System.Windows;
using Hearthstone_Deck_Tracker.Hearthstone;
using Deck = Hearthstone_Deck_Tracker.Hearthstone.Deck;

namespace Hearthstone_Deck_Tracker.Windows
{
	public partial class DeckCodeImportWindow : Window
	{
		public Deck? ImportedDeck { get; private set; }

		public DeckCodeImportWindow()
		{
			InitializeComponent();
			Loaded += (_, _) => DeckCodeInput.Focus();
		}

		private void AddDeck_OnClick(object sender, RoutedEventArgs e)
		{
			if(string.IsNullOrWhiteSpace(DeckCodeInput.Text))
			{
				ValidationMessage.Text = "Вставьте код колоды.";
				return;
			}

			try
			{
				var deck = DeckCodeUtility.Import(DeckCodeInput.Text.Trim());
				if(string.IsNullOrEmpty(deck.Class) || !deck.Cards.Any() || deck.Cards.Any(c => c.Count <= 0))
				{
					ValidationMessage.Text = "Не удалось прочитать карты и класс из кода.";
					return;
				}
				ImportedDeck = deck;
				DialogResult = true;
			}
			catch(Exception)
			{
				ValidationMessage.Text = "Неверный код колоды. Скопируйте его из Hearthstone и попробуйте ещё раз.";
			}
		}
	}
}
