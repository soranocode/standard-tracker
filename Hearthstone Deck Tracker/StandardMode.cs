using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Enums;

namespace Hearthstone_Deck_Tracker;

public static class StandardMode
{
	// Unknown formats fail closed: Constructed also includes Wild and Twist.
	public static bool IsSupported(FormatType format, GameMode mode)
		=> format == FormatType.FT_STANDARD
			&& (mode == GameMode.Ranked || mode == GameMode.Casual || mode == GameMode.Friendly);
}
