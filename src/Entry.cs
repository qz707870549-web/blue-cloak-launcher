namespace Luma;

public class Entry
{
	public string Id;

	public string Name;

	public string CustomName;

	public string CustomTarget;

	public bool Manual;

	public string Tags = "";

	public bool Favorite;

	public long LastLaunchTicks;

	public int LaunchCount;

	public string Target;

	public string Group = "未分类";

	public string FolderId;

	public bool FavoriteFolder;

	public bool Website;

	public bool Pinned;

	public int SortOrder = int.MaxValue;

	public bool TopPinned;

	public string CustomIcon;

	public string IconPath;

	public bool Utility;

	public bool SystemApp;

	public string ImagePath;
}
