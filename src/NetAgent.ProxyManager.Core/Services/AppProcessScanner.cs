using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class AppProcessScanner : IAppProcessScanner
{
    public Task<IReadOnlyList<RunningProcessInfo>> GetRunningProcessesAsync(CancellationToken cancellationToken)
    {
        var networkProcessIds = GetNetworkProcessIds();
        var wmiProcesses = TryGetWmiProcesses(networkProcessIds, cancellationToken);
        if (wmiProcesses.Count > 0)
        {
            return Task.FromResult<IReadOnlyList<RunningProcessInfo>>(wmiProcesses);
        }

        var processes = new List<RunningProcessInfo>();
        foreach (var process in Process.GetProcesses())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var executableName = $"{process.ProcessName}.exe";
                var executablePath = TryGetProcessImagePath(process.Id);
                processes.Add(new RunningProcessInfo(
                    process.Id,
                    executableName,
                    process.ProcessName,
                    string.IsNullOrWhiteSpace(process.MainWindowTitle) ? null : process.MainWindowTitle,
                    executablePath,
                    HasNetworkActivity: networkProcessIds.Contains(process.Id)));
            }
            catch (InvalidOperationException)
            {
                // The process can exit while being inspected.
            }
            finally
            {
                process.Dispose();
            }
        }

        IReadOnlyList<RunningProcessInfo> result = processes
            .OrderBy(process => process.ExecutableName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(process => process.ProcessId)
            .ToList();

        return Task.FromResult(result);
    }

    private static List<RunningProcessInfo> TryGetWmiProcesses(
        IReadOnlySet<int> networkProcessIds,
        CancellationToken cancellationToken)
    {
        var processes = new List<RunningProcessInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId,ParentProcessId,Name,ExecutablePath,CommandLine FROM Win32_Process");

            foreach (ManagementObject process in searcher.Get())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var processId = Convert.ToInt32(process["ProcessId"]);
                var executableName = Convert.ToString(process["Name"]) ?? $"{processId}.exe";
                var executablePath = Convert.ToString(process["ExecutablePath"]);
                executablePath = string.IsNullOrWhiteSpace(executablePath)
                    ? TryGetProcessImagePath(processId)
                    : executablePath;

                processes.Add(new RunningProcessInfo(
                    processId,
                    executableName,
                    Path.GetFileNameWithoutExtension(executableName),
                    GetMainWindowTitle(processId),
                    executablePath,
                    process["ParentProcessId"] is null ? null : Convert.ToInt32(process["ParentProcessId"]),
                    Convert.ToString(process["CommandLine"]),
                    networkProcessIds.Contains(processId)));
            }
        }
        catch
        {
            return [];
        }

        return processes
            .OrderBy(process => process.ExecutableName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(process => process.ProcessId)
            .ToList();
    }

    private static string? GetMainWindowTitle(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return string.IsNullOrWhiteSpace(process.MainWindowTitle) ? null : process.MainWindowTitle;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetProcessImagePath(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var builder = new StringBuilder(32768);
            var size = builder.Capacity;
            return QueryFullProcessImageName(handle, 0, builder, ref size)
                ? builder.ToString()
                : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    internal static IReadOnlySet<int> GetNetworkProcessIds()
    {
        var processIds = new HashSet<int>();
        AddTcpProcessIds(processIds);
        AddUdpProcessIds(processIds);
        return processIds;
    }

    private static void AddTcpProcessIds(HashSet<int> processIds)
    {
        var bufferSize = 0;
        var result = GetExtendedTcpTable(
            IntPtr.Zero,
            ref bufferSize,
            order: true,
            AfInet,
            TcpTableClass.TcpTableOwnerPidAll,
            reserved: 0);
        if (result is not ErrorInsufficientBuffer and not 0)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            result = GetExtendedTcpTable(
                buffer,
                ref bufferSize,
                order: true,
                AfInet,
                TcpTableClass.TcpTableOwnerPidAll,
                reserved: 0);
            if (result != 0)
            {
                return;
            }

            var count = Marshal.ReadInt32(buffer);
            var rowPointer = IntPtr.Add(buffer, sizeof(int));
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();

            for (var index = 0; index < count; index++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPointer);
                if (row.OwningPid > 0)
                {
                    processIds.Add(row.OwningPid);
                }

                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }
        }
        catch
        {
            // Process activity is a hint for rule creation; scanning should continue without it.
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void AddUdpProcessIds(HashSet<int> processIds)
    {
        var bufferSize = 0;
        var result = GetExtendedUdpTable(
            IntPtr.Zero,
            ref bufferSize,
            order: true,
            AfInet,
            UdpTableClass.UdpTableOwnerPid,
            reserved: 0);
        if (result is not ErrorInsufficientBuffer and not 0)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            result = GetExtendedUdpTable(
                buffer,
                ref bufferSize,
                order: true,
                AfInet,
                UdpTableClass.UdpTableOwnerPid,
                reserved: 0);
            if (result != 0)
            {
                return;
            }

            var count = Marshal.ReadInt32(buffer);
            var rowPointer = IntPtr.Add(buffer, sizeof(int));
            var rowSize = Marshal.SizeOf<MibUdpRowOwnerPid>();

            for (var index = 0; index < count; index++)
            {
                var row = Marshal.PtrToStructure<MibUdpRowOwnerPid>(rowPointer);
                if (row.OwningPid > 0)
                {
                    processIds.Add(row.OwningPid);
                }

                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }
        }
        catch
        {
            // Process activity is a hint for rule creation; scanning should continue without it.
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private const int AfInet = 2;
    private const uint ErrorInsufficientBuffer = 122;
    private const uint ProcessQueryLimitedInformation = 0x1000;

    private enum TcpTableClass
    {
        TcpTableOwnerPidAll = 5
    }

    private enum UdpTableClass
    {
        UdpTableOwnerPid = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MibTcpRowOwnerPid
    {
        public readonly uint State;
        public readonly uint LocalAddress;
        private readonly uint _localPort;
        public readonly uint RemoteAddress;
        private readonly uint _remotePort;
        public readonly int OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MibUdpRowOwnerPid
    {
        public readonly uint LocalAddress;
        private readonly uint _localPort;
        public readonly int OwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int tcpTableLength,
        bool order,
        int ipVersion,
        TcpTableClass tableClass,
        int reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(
        IntPtr udpTable,
        ref int udpTableLength,
        bool order,
        int ipVersion,
        UdpTableClass tableClass,
        int reserved);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(
        IntPtr process,
        int flags,
        StringBuilder executablePath,
        ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
