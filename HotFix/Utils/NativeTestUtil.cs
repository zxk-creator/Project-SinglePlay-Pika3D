using System.Runtime.InteropServices;

public static class NativeTestUtil
{

    private const string DllName = "UnityNativeTest";

    [DllImport(
        DllName,
        EntryPoint = "UnityNativeTest_Add",
        CallingConvention = CallingConvention.Cdecl)]
    public static extern int AddNative(int a, int b);

    [DllImport(
        DllName,
        EntryPoint = "UnityNativeTest_TryCrash",
        CallingConvention = CallingConvention.Cdecl)]
    public static extern void TryCrashNative();
}
