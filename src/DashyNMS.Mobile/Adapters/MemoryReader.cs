using System.Runtime.InteropServices;
using DashyNMS.Mobile.Diagnostics;

namespace DashyNMS.Mobile.Adapters;

/// <summary>
/// The app's memory as each OS counts it, for the opt-in measurements (#219):
/// iOS's physical footprint - what it ends apps over - and Android's PSS.
/// Totals for this process only.
/// </summary>
public static class MemoryReader
{
    public static MemoryReading Read() => new(Footprint(), GC.GetTotalMemory(forceFullCollection: false));

#if IOS
    // task_info(TASK_VM_INFO): phys_footprint is the 64-bit field at byte
    // 144 of task_vm_info_data_t (index 18 in 64-bit words), present since
    // revision 1 of the struct.
    private const int TaskVmInfo = 22;
    private const int FootprintIndex = 18;

#pragma warning disable SYSLIB1054 // A plain long[] marshals as is; LibraryImport would need unsafe code here.
    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern uint mach_task_self();

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern int task_info(uint task, int flavor, [Out] long[] info, ref uint count);
#pragma warning restore SYSLIB1054

    private static long? Footprint()
    {
        var info = new long[64];
        var count = (uint)(info.Length * sizeof(long) / sizeof(uint));
        if (task_info(mach_task_self(), TaskVmInfo, info, ref count) != 0
            || count * sizeof(uint) < (FootprintIndex + 1) * sizeof(long))
        {
            return null;
        }

        return info[FootprintIndex];
    }
#elif ANDROID
    private static long? Footprint() => Android.OS.Debug.Pss * 1024;
#else
    private static long? Footprint() => null;
#endif
}
