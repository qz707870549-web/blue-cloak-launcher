using System;
namespace Luma;
public static class KeyboardInputIdentity
{
    public static readonly UIntPtr Tag = new UIntPtr(0x42434c57u);
    public static bool IsOwn(UIntPtr tag) => tag == Tag;
}
