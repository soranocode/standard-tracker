#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Serialization;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Library;

#endregion

namespace Hearthstone_Deck_Tracker
{
	[XmlRoot(ElementName = "Decks")]
	public class DeckList
	{
		private static Lazy<DeckList> _instance = new Lazy<DeckList>(Load);
		private Deck? _activeDeck;
		internal Guid LibraryId { get; set; }

		[XmlArray(ElementName = "Tags")]
		[XmlArrayItem(ElementName = "Tag")]
		public List<string> AllTags;

		[XmlElement(ElementName = "Deck")]
		public ObservableCollection<Deck> Decks;

		public List<DeckInfo> LastDeckClass;

		public DeckList()
		{
			Decks = new ObservableCollection<Deck>();
			AllTags = new List<string>();
			LastDeckClass = new List<DeckInfo>();
		}

		public event Action<Deck?>? ActiveDeckChanged;

		[XmlIgnore]
		public Deck? ActiveDeck
		{
			get { return _activeDeck; }
			set
			{
				if(Equals(_activeDeck, value) && _activeDeck?.Version == value?.Version)
					return;

				if(_activeDeck != null)
					_activeDeck.SelectedVersionChanged -= OnActiveDeckChanged;
				if(value != null)
					value.SelectedVersionChanged += OnActiveDeckChanged;
				_activeDeck = value;
				OnActiveDeckChanged();
			}
		}

		private void OnActiveDeckChanged()
		{
			Log.Info("Set active deck to: " + (ActiveDeck == null ? "null" : $"{ActiveDeck.Name} ({ActiveDeck.SelectedVersion.ShortVersionString})"));
			Config.Instance.ActiveDeckId = ActiveDeck?.DeckId ?? Guid.Empty;
			if(ActiveDeck != null)
				UpdateLastDeckClass(ActiveDeck);
			ActiveDeckChanged?.Invoke(ActiveDeck);
		}

		public Deck? ActiveDeckVersion => ActiveDeck?.GetSelectedDeckVersion();

		public static DeckList Instance => _instance.Value;

		private void LoadActiveDeck()
		{
			var deck = Decks.FirstOrDefault(d => d.DeckId == Config.Instance.ActiveDeckId);
			if(deck != null && deck.Archived)
				deck = null;
			ActiveDeck = deck;
		}

		public Deck? GetLastUsedDeck()
		{
			var lastSelected = LastDeckClass.LastOrDefault();
			return Decks.FirstOrDefault(d => lastSelected == null || d.DeckId == lastSelected.Id);
		}

		private void UpdateLastDeckClass(Deck deck)
		{
			while(LastDeckClass.Any(ldc => ldc.Class == deck.Class))
			{
				var lastSelected = LastDeckClass.FirstOrDefault(ldc => ldc.Class == deck.Class);
				if(lastSelected != null)
					LastDeckClass.Remove(lastSelected);
				else
					break;
			}
			if(!Core.Initialized)
				return;
			LastDeckClass.Add(new DeckInfo { Class = deck.Class, Name = deck.Name, Id = deck.DeckId });
			Save(this);
		}

		internal static DeckList? LoadedInstance => _instance.IsValueCreated ? _instance.Value : null;

		private static DeckList Load()
		{
			var instance = StandardLibrarySession.Current.Decks;
			instance.LoadActiveDeck();
			return instance;
		}

#if(!SQUIRREL)
		internal static void SetupDeckListFile()
		{
			// Profiles are isolated. Legacy file migration is intentionally unavailable.
		}

#endif
		private static void Save(DeckList instance) => StandardLibrarySession.Save(decks: instance);
		public static void Save() => Save(Instance);

		internal static void Reload()
		{
			StandardLibrarySession.Reset();
			_instance = new Lazy<DeckList>(Load);
		}
	}

	public class DeckInfo
	{
		public string? Class;
		public Guid Id;
		public string? Name;
	}
}
