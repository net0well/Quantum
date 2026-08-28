using System.Runtime.InteropServices;

namespace Quantum.Audio.Interop;

/// <summary>
/// Leitura e escrita tipada do property store de um endpoint, sempre liberando
/// o PROPVARIANT devolvido pelo COM.
/// </summary>
internal static class DeviceProperties
{
    public static string? GetString(IMMDevice device, PropertyKey key)
    {
        return Read(device, key, static v => v.AsString());
    }

    public static uint? GetUInt32(IMMDevice device, PropertyKey key)
    {
        return Read(device, key, static v => v.AsUInt32());
    }

    public static byte[]? GetBlob(IMMDevice device, PropertyKey key)
    {
        return Read(device, key, static v => v.AsBlob());
    }

    /// <summary>
    /// Grava um valor no property store do endpoint. Exige elevação na maior parte
    /// das chaves — devolve o HRESULT para o chamador decidir o que mostrar.
    /// </summary>
    public static int SetBlob(IMMDevice device, PropertyKey key, byte[] data)
    {
        var hr = device.OpenPropertyStore(Stgm.ReadWrite, out var store);
        if (!HResults.Ok(hr) || store is null)
        {
            return hr;
        }

        var buffer = PropVariantNative.CoTaskMemAlloc((nuint)data.Length);
        if (buffer == 0)
        {
            return unchecked((int)0x8007000E); // E_OUTOFMEMORY
        }

        try
        {
            Marshal.Copy(data, 0, buffer, data.Length);
            var value = new PropVariant
            {
                VariantType = PropVariant.VT_BLOB,
                Data0 = data.Length,
                Data1 = buffer,
            };

            hr = store.SetValue(ref key, ref value);
            if (HResults.Ok(hr))
            {
                hr = store.Commit();
            }

            return hr;
        }
        finally
        {
            PropVariantNative.CoTaskMemFree(buffer);
            Marshal.ReleaseComObject(store);
        }
    }

    public static int SetUInt32(IMMDevice device, PropertyKey key, uint data) =>
        SetScalar(device, key, PropVariant.FromUInt32(data));

    public static int SetUInt16(IMMDevice device, PropertyKey key, ushort data) =>
        SetScalar(device, key, PropVariant.FromUInt16(data));

    private static int SetScalar(IMMDevice device, PropertyKey key, PropVariant value)
    {
        var hr = device.OpenPropertyStore(Stgm.ReadWrite, out var store);
        if (!HResults.Ok(hr) || store is null)
        {
            return hr;
        }

        try
        {
            hr = store.SetValue(ref key, ref value);
            if (HResults.Ok(hr))
            {
                hr = store.Commit();
            }

            return hr;
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private static T? Read<T>(IMMDevice device, PropertyKey key, Func<PropVariant, T?> selector)
    {
        if (!HResults.Ok(device.OpenPropertyStore(Stgm.Read, out var store)) || store is null)
        {
            return default;
        }

        var value = default(PropVariant);
        try
        {
            return !HResults.Ok(store.GetValue(ref key, out value)) ? default : selector(value);
        }
        finally
        {
            if (!value.IsEmpty)
            {
                PropVariantNative.PropVariantClear(ref value);
            }

            Marshal.ReleaseComObject(store);
        }
    }
}
