using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Enums;

namespace Hearthstone_Deck_Tracker;

public static class StandardMode
{
	// Only confirmed Standard and Wild constructed queues are supported.
	public static bool IsSupported(FormatType format, GameMode mode)
		=> (format == FormatType.FT_STANDARD || format == FormatType.FT_WILD)
			&& (mode == GameMode.Ranked || mode == GameMode.Casual || mode == GameMode.Friendly);
}
