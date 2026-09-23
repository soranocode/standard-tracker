using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HearthDb.Deckstrings;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using CardType = HearthDb.Enums.CardType;
using TrackerDeck = Hearthstone_Deck_Tracker.Hearthstone.Deck;

namespace Hearthstone_Deck_Tracker.Controls
{
	public partial class SelectedDeckPanel : UserControl
	{
		public SelectedDeckPanelViewModel ViewModel { get; } = new();

		public SelectedDeckPanel()
		{
			InitializeComponent();
			DataContext = ViewModel;
		}

		public void SetDeck(TrackerDeck? deck)
		{
			ViewModel.SetDeck(deck);
			ManaCurveMyDecks.SetDeck(deck);
			ManaCurveMyDecks.Visibility = Config.Instance.ManaCurveMyDecks && deck != null
				? Visibility.Visible
				: Visibility.Collapsed;
		}

		public void UpdateValues() => ManaCurveMyDecks.UpdateValues();
	}

	public class SelectedDeckPanelViewModel : INotifyPropertyChanged
	{
		private TrackerDeck? _deck;
		private string _searchText = "";
		private string _typeFilter = "all";
		private List<Hearthstone.Card> _allCards = new();

		public event PropertyChangedEventHandler? PropertyChanged;

		public ObservableCollection<Hearthstone.Card> VisibleCards { get; } = new();

		public bool HasDeck => _deck != null;
		public bool HasCards => VisibleCards.Count > 0;
		public Visibility EmptyVisibility => HasCards ? Visibility.Collapsed : Visibility.Visible;

		public string DeckName => _deck?.NameAndVersion ?? "Колода не выбрана";
		public string ClassName => _deck?.Class ?? "";
		public string FormatLabel => _deck == null ? "" : (_deck.StandardViable ? "Standard" : "Wild");
		public BitmapImage? ClassImage => _deck?.ClassImage;
		public SolidColorBrush? ClassColorBrush => _deck?.ClassColorBrush;

		public string DeckCode
		{
			get
			{
				if(_deck == null)
					return "";
				try
				{
					var hearthDbDeck = HearthDbConverter.ToHearthDbDeck(_deck.GetSelectedDeckVersion());
					return hearthDbDeck == null ? "" : DeckSerializer.Serialize(hearthDbDeck, false);
				}
				catch
				{
					return "";
				}
			}
		}

		public int TotalCards => _allCards.Sum(c => c.Count);
		public int MinionCards => _allCards.Where(c => c.TypeEnum == CardType.MINION).Sum(c => c.Count);
		public int SpellCards => _allCards.Where(c => c.TypeEnum == CardType.SPELL).Sum(c => c.Count);

		public string ShownLabel
		{
			get
			{
				if(_deck == null)
					return "";
				var shown = VisibleCards.Sum(c => c.Count);
				return $"Показано {shown} из {TotalCards}";
			}
		}

		public string EmptyTitle => _deck == null ? "Выберите колоду" : "Таких карт нет";
		public string EmptySubtitle => _deck == null
			? "Откройте колоду в библиотеке слева."
			: "Измените поиск, тип или стоимость.";

		public string SearchText
		{
			get => _searchText;
			set
			{
				if(_searchText == value)
					return;
				_searchText = value;
				RefreshVisible();
				OnPropertyChanged();
			}
		}

		public bool FilterAll
		{
			get => _typeFilter == "all";
			set
			{
				if(value)
					SetTypeFilter("all");
			}
		}

		public bool FilterMinions
		{
			get => _typeFilter == "minion";
			set
			{
				if(value)
					SetTypeFilter("minion");
			}
		}

		public bool FilterSpells
		{
			get => _typeFilter == "spell";
			set
			{
				if(value)
					SetTypeFilter("spell");
			}
		}

		public void SetDeck(TrackerDeck? deck)
		{
			_deck = deck;
			_searchText = "";
			_typeFilter = "all";
			_allCards = deck == null
				? new List<Hearthstone.Card>()
				: Helper.ResolveZilliax3000(deck.GetSelectedDeckVersion().Cards, deck.GetSelectedDeckVersion().Sideboards)
					.ToSortedCardList()
					.ToList();
			RefreshVisible();
			OnPropertyChanged(nameof(HasDeck));
			OnPropertyChanged(nameof(DeckName));
			OnPropertyChanged(nameof(ClassName));
			OnPropertyChanged(nameof(FormatLabel));
			OnPropertyChanged(nameof(ClassImage));
			OnPropertyChanged(nameof(ClassColorBrush));
			OnPropertyChanged(nameof(DeckCode));
			OnPropertyChanged(nameof(TotalCards));
			OnPropertyChanged(nameof(MinionCards));
			OnPropertyChanged(nameof(SpellCards));
			OnPropertyChanged(nameof(SearchText));
			OnPropertyChanged(nameof(FilterAll));
			OnPropertyChanged(nameof(FilterMinions));
			OnPropertyChanged(nameof(FilterSpells));
			OnPropertyChanged(nameof(EmptyTitle));
			OnPropertyChanged(nameof(EmptySubtitle));
		}

		private void SetTypeFilter(string filter)
		{
			_typeFilter = filter;
			RefreshVisible();
			OnPropertyChanged(nameof(FilterAll));
			OnPropertyChanged(nameof(FilterMinions));
			OnPropertyChanged(nameof(FilterSpells));
		}

		private void RefreshVisible()
		{
			VisibleCards.Clear();
			IEnumerable<Hearthstone.Card> query = _allCards;
			if(_typeFilter == "minion")
				query = query.Where(c => c.TypeEnum == CardType.MINION);
			else if(_typeFilter == "spell")
				query = query.Where(c => c.TypeEnum == CardType.SPELL);

			var text = (_searchText ?? "").Trim();
			if(text.Length > 0)
				query = query.Where(c => (c.LocalizedName ?? c.Name ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);

			foreach(var card in query)
				VisibleCards.Add(card);

			OnPropertyChanged(nameof(HasCards));
			OnPropertyChanged(nameof(EmptyVisibility));
			OnPropertyChanged(nameof(ShownLabel));
		}

		private void OnPropertyChanged([CallerMemberName] string? name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
