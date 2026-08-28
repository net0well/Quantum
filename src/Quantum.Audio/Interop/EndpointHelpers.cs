using System.Runtime.InteropServices;

namespace Quantum.Audio.Interop;

/// <summary>Boilerplate de abrir endpoint, ativar interface e liberar, num lugar só.</summary>
internal static class EndpointHelpers
{
    public static IMMDevice? Open(IMMDeviceEnumerator enumerator, string deviceId)
    {
        try
        {
            return HResults.Ok(enumerator.GetDevice(deviceId, out var device)) ? device : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public static T? Activate<T>(IMMDevice device, Guid interfaceId)
        where T : class
    {
        try
        {
            return HResults.Ok(device.Activate(ref interfaceId, ComIids.CLSCTX_ALL, 0, out var raw))
                ? raw as T
                : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public static string? GetDefaultId(IMMDeviceEnumerator enumerator, DataFlow flow, Role role)
    {
        if (!HResults.Ok(enumerator.GetDefaultAudioEndpoint(flow, role, out var device)) || device is null)
        {
            return null;
        }

        try
        {
            return HResults.Ok(device.GetId(out var id)) ? id : null;
        }
        finally
        {
            Release(device);
        }
    }

    public static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }
}
