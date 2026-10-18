using System;

namespace Luma;

public sealed class AppFolder
{
	public string Id = Guid.NewGuid().ToString("N");

	public string Name = "新建文件夹";

	public string Group = "未分类";

	public bool InWorkspace;
}
