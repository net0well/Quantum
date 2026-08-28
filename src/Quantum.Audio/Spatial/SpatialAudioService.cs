using System.Runtime.InteropServices;
using System.Text;
using Quantum.Audio.Interop;
using Quantum.Audio.Models;

namespace Quantum.Audio.Spatial;

/// <summary>
/// O Windows não publica API para áudio espacial — a seleção vive no property store
/// do endpoint. O layout abaixo foi levantado comparando todos os endpoints da máquina:
/// a chave de seleção tem o mesmo valor em todos quando está desligada, enquanto a de
/// contagem é constante e portanto não pode ser a seleção.
///
/// Cada entrada do catálogo termina com um bloco fixo:
///   [GUID do provedor : 16] [flag de disponibilidade : 4] [3 DWORDs : 12] [id do formato : 4]
///   [WAVEFORMAT resumido : 30]
/// Por isso o id fica sempre a 34 bytes do fim e a flag a 50.
/// </summary>
public sealed class SpatialAudioService : ISpatialAudioService
{
    private const int IdOffsetFromEnd = 34;
    private const int AvailabilityOffsetFromEnd = 50;
    private const int MaxCatalogEntries = 32;

    private readonly IMMDeviceEnumerator _enumerator;

    public SpatialAudioService()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
    }

    public IReadOnlyList<SpatialFormatInfo> GetFormats(string deviceId)
    {
        var device = Open(deviceId);
        if (device is null)
        {
            return [SpatialFormatInfo.Disabled];
        }

        try
        {
            var formats = new List<SpatialFormatInfo> { SpatialFormatInfo.Disabled };
            var declared = DeviceProperties.GetUInt32(device, PropertyKeys.SpatialFormatCount) ?? 0;
            var limit = Math.Min(declared == 0 ? MaxCatalogEntries : declared + 2, MaxCatalogEntries);

            for (var i = 0; i < limit; i++)
            {
                var key = new PropertyKey(
                    PropertyKeys.SpatialFormatCatalog,
                    PropertyKeys.SpatialCatalogFirstPid + i);

                var blob = DeviceProperties.GetBlob(device, key);
                if (blob is null)
                {
                    continue;
                }

                var entry = ParseCatalogEntry(blob);
                if (entry is not null && formats.All(f => f.Id != entry.Id))
                {
                    formats.Add(entry);
                }
            }

            return formats;
        }
        finally
        {
            Release(device);
        }
    }

    public SpatialFormatInfo GetCurrentFormat(string deviceId)
    {
        var device = Open(deviceId);
        if (device is null)
        {
            return SpatialFormatInfo.Disabled;
        }

        try
        {
            var currentId = DeviceProperties.GetUInt32(device, PropertyKeys.SpatialCurrentFormat) ?? 0;
            if (currentId == SpatialFormatInfo.DisabledId)
            {
                return SpatialFormatInfo.Disabled;
            }

            return GetFormats(deviceId).FirstOrDefault(f => f.Id == currentId)
                   ?? new SpatialFormatInfo(currentId, $"Formato espacial {currentId}", true);
        }
        finally
        {
            Release(device);
        }
    }

    public AudioResult SetFormat(string deviceId, SpatialFormatInfo format)
    {
        if (!format.IsAvailable && !format.IsDisabled)
        {
            return AudioResult.Fail(
                $"\"{format.Name}\" precisa do app correspondente instalado e licenciado na Microsoft Store.");
        }

        var device = Open(deviceId);
        if (device is null)
        {
            return AudioResult.Fail("Dispositivo não encontrado.");
        }

        try
        {
            var hr = DeviceProperties.SetUInt16(device, PropertyKeys.SpatialCurrentFormat, (ushort)format.Id);
            if (!HResults.Ok(hr))
            {
                return hr == HResults.E_ACCESSDENIED
                    ? AudioResult.Fail(hr, "Trocar o áudio espacial exige executar o Quantum como administrador.")
                    : AudioResult.Fail(hr, $"O Windows recusou a mudança (0x{hr:X8}).");
            }

            // Confere de fato — escrever no property store nem sempre significa que pegou.
            var applied = DeviceProperties.GetUInt32(device, PropertyKeys.SpatialCurrentFormat) ?? 0;
            if (applied != format.Id)
            {
                return AudioResult.Fail(
                    "A gravação foi aceita mas o Windows manteve o valor anterior. " +
                    "Use o botão \"Som do Windows\" para trocar pelo painel nativo.");
            }

            return AudioResult.Ok(format.IsDisabled
                ? "Áudio espacial desligado."
                : $"Áudio espacial: {format.Name}.");
        }
        catch (COMException ex)
        {
            return AudioResult.Fail(ex.HResult, "Falha ao falar com o dispositivo.");
        }
        finally
        {
            Release(device);
        }
    }

    /// <summary>Extrai nome, disponibilidade e id de uma entrada do catálogo.</summary>
    internal static SpatialFormatInfo? ParseCatalogEntry(byte[] blob)
    {
        if (blob.Length < IdOffsetFromEnd + 4)
        {
            return null;
        }

        var name = ReadNullTerminatedUnicode(blob);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var id = BitConverter.ToUInt32(blob, blob.Length - IdOffsetFromEnd);
        if (id is 0 or > 64)
        {
            return null;
        }

        var isAvailable = blob.Length >= AvailabilityOffsetFromEnd + 4 &&
                          BitConverter.ToUInt32(blob, blob.Length - AvailabilityOffsetFromEnd) != 0;

        return new SpatialFormatInfo(id, name, isAvailable);
    }

    private static string ReadNullTerminatedUnicode(byte[] blob)
    {
        var end = 0;
        while (end + 1 < blob.Length && (blob[end] != 0 || blob[end + 1] != 0))
        {
            end += 2;
        }

        return end == 0 ? string.Empty : Encoding.Unicode.GetString(blob, 0, end);
    }

    private IMMDevice? Open(string deviceId) =>
        HResults.Ok(_enumerator.GetDevice(deviceId, out var device)) ? device : null;

    private static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }
}
