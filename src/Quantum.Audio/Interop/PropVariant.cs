using System.Runtime.InteropServices;

namespace Quantum.Audio.Interop;

/// <summary>PROPERTYKEY: GUID do formato + id numérico da propriedade.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct PropertyKey(Guid formatId, int propertyId)
{
    public Guid FormatId = formatId;
    public int PropertyId = propertyId;

    public PropertyKey(string formatId, int propertyId) : this(new Guid(formatId), propertyId) { }
}

/// <summary>
/// PROPVARIANT. O layout precisa bater byte a byte com a struct nativa
/// (24 bytes em x64, 16 em x86) — um tamanho menor faz a chamada COM corromper a pilha.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
#pragma warning disable CS0649 // atribuído pelo marshaller COM
    public ushort VariantType;
    public ushort Reserved1;
    public ushort Reserved2;
    public ushort Reserved3;
    public nint Data0;
    public nint Data1;
#pragma warning restore CS0649

    public const ushort VT_EMPTY = 0;
    public const ushort VT_UI2 = 18;
    public const ushort VT_UI4 = 19;
    public const ushort VT_LPWSTR = 31;
    public const ushort VT_BLOB = 65;

    public readonly bool IsEmpty => VariantType == VT_EMPTY;

    /// <summary>Lê a propriedade como string (VT_LPWSTR).</summary>
    public readonly string? AsString() =>
        VariantType == VT_LPWSTR && Data0 != 0 ? Marshal.PtrToStringUni(Data0) : null;

    /// <summary>Lê a propriedade como inteiro sem sinal (VT_UI2 / VT_UI4).</summary>
    public readonly uint? AsUInt32() => VariantType switch
    {
        VT_UI2 => (uint)(Data0 & 0xFFFF),
        VT_UI4 => (uint)(Data0 & 0xFFFFFFFF),
        _ => null,
    };

    /// <summary>Copia o conteúdo de um VT_BLOB para um array gerenciado.</summary>
    public readonly byte[]? AsBlob()
    {
        if (VariantType != VT_BLOB)
        {
            return null;
        }

        var size = (int)(Data0 & 0x7FFFFFFF);
        if (size <= 0 || Data1 == 0)
        {
            return null;
        }

        var buffer = new byte[size];
        Marshal.Copy(Data1, buffer, 0, size);
        return buffer;
    }

    /// <summary>Monta um PROPVARIANT VT_UI4 (a memória do valor é inline, não precisa liberar).</summary>
    public static PropVariant FromUInt32(uint value) =>
        new() { VariantType = VT_UI4, Data0 = (nint)value };

    /// <summary>Monta um PROPVARIANT VT_UI2 — o tipo usado pela seleção de áudio espacial.</summary>
    public static PropVariant FromUInt16(ushort value) =>
        new() { VariantType = VT_UI2, Data0 = value };
}

internal static class PropVariantNative
{
    [DllImport("ole32.dll")]
    public static extern int PropVariantClear(ref PropVariant pvar);

    [DllImport("ole32.dll")]
    public static extern nint CoTaskMemAlloc(nuint cb);

    [DllImport("ole32.dll")]
    public static extern void CoTaskMemFree(nint pv);
}
