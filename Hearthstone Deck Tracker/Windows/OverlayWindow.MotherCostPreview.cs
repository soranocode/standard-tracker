using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using Hearthstone_Deck_Tracker.Utility;

namespace Hearthstone_Deck_Tracker.Windows;

public partial class OverlayWindow
{
	private const string MotherCardId = "BE_036";
	private readonly List<(Border Badge, TextBlock Cost)> _motherCostBadges = new();
	private MotherPreviewPhase _motherPreviewPhase;
	private int _motherEntityId;
	private DateTime _motherPreviewStarted;
	private bool _motherWaitingForBoardClick;
	private bool _motherLeftWasDown;
	private bool _motherRightWasDown;

	private enum MotherPreviewPhase { Idle, CardSelected, ChoosingTarget }

	private void UpdateMotherCostPreview()
	{
		var leftDown = User32.IsLeftMouseButtonDown();
		var rightDown = User32.IsRightMouseButtonDown();
		var leftPressed = leftDown && !_motherLeftWasDown;
		var leftReleased = !leftDown && _motherLeftWasDown;
		var rightPressed = rightDown && !_motherRightWasDown;
		_motherLeftWasDown = leftDown;
		_motherRightWasDown = rightDown;

		if(!User32.IsHearthstoneInForeground() || !IsContentVisible || !_game.IsTraditionalHearthstoneMatch
		   || !_game.IsMulliganDone || IsGameOver)
		{
			ClearMotherCostPreview();
			return;
		}

		var cursor = GetCursorPos();
		if(cursor == null || rightPressed || (_motherPreviewPhase != MotherPreviewPhase.Idle
		   && DateTime.UtcNow - _motherPreviewStarted > TimeSpan.FromSeconds(45)))
		{
			ClearMotherCostPreview();
			return;
		}
		if(_motherPreviewPhase == MotherPreviewPhase.Idle && !leftPressed)
			return;

		var hand = _game.Player.Hand.OrderBy(e => e.GetTag(GameTag.ZONE_POSITION)).Take(MaxHandSize).ToList();
		if(_motherPreviewPhase == MotherPreviewPhase.Idle)
		{
			if(leftPressed)
			{
				var sourceIndex = FindMotherHandCard(hand, cursor.Value);
				if(sourceIndex >= 0 && hand[sourceIndex].CardId == MotherCardId)
				{
					_motherEntityId = hand[sourceIndex].Id;
					_motherPreviewStarted = DateTime.UtcNow;
					_motherPreviewPhase = MotherPreviewPhase.CardSelected;
				}
			}
			return;
		}

		if(_motherPreviewPhase == MotherPreviewPhase.CardSelected)
		{
			if(leftReleased)
			{
				if(IsMotherBoardDrop(cursor.Value))
					_motherPreviewPhase = MotherPreviewPhase.ChoosingTarget;
				else
					_motherWaitingForBoardClick = true;
			}
			else if(leftPressed && _motherWaitingForBoardClick)
			{
				if(IsMotherBoardDrop(cursor.Value))
					_motherPreviewPhase = MotherPreviewPhase.ChoosingTarget;
				else
					ClearMotherCostPreview();
			}
			else if(!leftDown && hand.All(e => e.Id != _motherEntityId))
				_motherPreviewPhase = MotherPreviewPhase.ChoosingTarget;
			if(_motherPreviewPhase != MotherPreviewPhase.ChoosingTarget)
				return;
		}

		// The source can still appear in Power.log until the target is confirmed. It has
		// already left the visible hand at this point, so exclude it from the preview.
		var remaining = hand.Where(e => e.Id != _motherEntityId).ToList();
		var targetIndex = FindMotherTarget(remaining.Count, cursor.Value);
		if(leftPressed && targetIndex >= 0)
		{
			ClearMotherCostPreview();
			return;
		}
		ShowMotherCosts(remaining, targetIndex);
	}

	private bool IsMotherBoardDrop(Point cursor) => cursor.Y > Height * 0.35 && cursor.Y < Height * 0.82;

	private int FindMotherHandCard(IReadOnlyList<Entity> hand, Point cursor)
	{
		for(var i = hand.Count - 1; i >= 0; i--)
			if(RotatedRectContains(_playerHand[i], cursor))
				return i;
		return FindMotherTarget(hand.Count, cursor);
	}

	private int FindMotherTarget(int count, Point cursor)
	{
		if(count == 0 || cursor.Y < Height * 0.65 || cursor.Y > Height * 1.05)
			return -1;
		var nearest = -1;
		var distance = double.MaxValue;
		for(var i = 0; i < count; i++)
		{
			var dx = Math.Abs(GetPlayerCardPosition(i, count).X - cursor.X);
			if(dx < distance)
			{
				distance = dx;
				nearest = i;
			}
		}
		return distance <= CardWidth * 0.65 ? nearest : -1;
	}

	private void ShowMotherCosts(IReadOnlyList<Entity> hand, int targetIndex)
	{
		EnsureMotherCostBadges();
		var size = Math.Max(32, Math.Min(56, Height * 0.05));
		for(var i = 0; i < _motherCostBadges.Count; i++)
		{
			var (badge, cost) = _motherCostBadges[i];
			var reduction = targetIndex < 0 ? 0 : 5 - Math.Abs(i - targetIndex);
			if(i >= hand.Count || reduction <= 0)
			{
				badge.Visibility = Visibility.Collapsed;
				continue;
			}
			var position = GetPlayerCardPosition(i, hand.Count);
			cost.Text = Math.Max(0, hand[i].Cost - reduction).ToString();
			cost.FontSize = size * 0.58;
			badge.Width = badge.Height = size;
			badge.CornerRadius = new CornerRadius(size / 2);
			badge.BorderThickness = i == targetIndex ? new Thickness(3) : new Thickness(1.5);
			Canvas.SetLeft(badge, position.X - CardWidth / 2 - size * 0.15);
			Canvas.SetTop(badge, position.Y - CardHeight / 2 - (i == targetIndex ? CardHeight * 0.25 : 0));
			badge.Visibility = Visibility.Visible;
		}
	}

	private void EnsureMotherCostBadges()
	{
		if(_motherCostBadges.Count > 0)
			return;
		for(var i = 0; i < MaxHandSize; i++)
		{
			var cost = new TextBlock
			{
				Foreground = Brushes.LawnGreen,
				FontWeight = FontWeights.Bold,
				TextAlignment = TextAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			var badge = new Border
			{
				Child = cost,
				Background = new SolidColorBrush(Color.FromArgb(238, 17, 35, 33)),
				BorderBrush = Brushes.LawnGreen,
				IsHitTestVisible = false,
				Visibility = Visibility.Collapsed
			};
			MotherCostPreview.Children.Add(badge);
			_motherCostBadges.Add((badge, cost));
		}
	}

	private void ClearMotherCostPreview()
	{
		_motherPreviewPhase = MotherPreviewPhase.Idle;
		_motherEntityId = 0;
		_motherWaitingForBoardClick = false;
		foreach(var (badge, _) in _motherCostBadges)
			badge.Visibility = Visibility.Collapsed;
	}
}
