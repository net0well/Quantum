using System.Runtime.InteropServices;

namespace Quantum.Audio.Interop;

/// <summary>WAVEFORMATEX — 18 bytes, exige Pack = 1 para não ganhar padding.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct WaveFormatEx
{
    public ushort FormatTag;
    public ushort Channels;
    public uint SamplesPerSec;
    public uint AvgBytesPerSec;
    public ushort BlockAlign;
    public ushort BitsPerSample;
    public ushort ExtraSize;

    public const ushort WAVE_FORMAT_PCM = 1;
    public const ushort WAVE_FORMAT_IEEE_FLOAT = 3;
    public const ushort WAVE_FORMAT_EXTENSIBLE = 0xFFFE;
}

/// <summary>WAVEFORMATEXTENSIBLE — 40 bytes (18 + 2 + 4 + 16).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct WaveFormatExtensible
{
    public WaveFormatEx Format;
    public ushort ValidBitsPerSample;
    public uint ChannelMask;
    public Guid SubFormat;

    public static readonly Guid SubTypePcm = new("00000001-0000-0010-8000-00aa00389b71");
    public static readonly Guid SubTypeIeeeFloat = new("00000003-0000-0010-8000-00aa00389b71");

    public const uint SPEAKER_FRONT_LEFT = 0x1;
    public const uint SPEAKER_FRONT_RIGHT = 0x2;

    /// <summary>Monta um formato PCM inteiro em modo compartilhado.</summary>
    public static WaveFormatExtensible CreatePcm(int sampleRate, int bitsPerSample, int channels)
    {
        var blockAlign = (ushort)(channels * (bitsPerSample / 8));
        return new WaveFormatExtensible
        {
            Format = new WaveFormatEx
            {
                FormatTag = WaveFormatEx.WAVE_FORMAT_EXTENSIBLE,
                Channels = (ushort)channels,
                SamplesPerSec = (uint)sampleRate,
                AvgBytesPerSec = (uint)(sampleRate * blockAlign),
                BlockAlign = blockAlign,
                BitsPerSample = (ushort)bitsPerSample,
                ExtraSize = 22,
            },
            ValidBitsPerSample = (ushort)bitsPerSample,
            ChannelMask = ChannelMaskFor(channels),
            SubFormat = SubTypePcm,
        };
    }

    private static uint ChannelMaskFor(int channels) => channels switch
    {
        1 => SPEAKER_FRONT_LEFT,
        2 => SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT,
        6 => 0x3F,   // 5.1
        8 => 0x63F,  // 7.1
        _ => (uint)((1 << channels) - 1),
    };

    /// <summary>
    /// Lê um WAVEFORMATEX(TENSIBLE) de um blob bruto do registro/property store.
    /// Retorna a taxa, a profundidade real de bits e o número de canais.
    /// </summary>
    public static (int SampleRate, int BitsPerSample, int Channels)? Parse(ReadOnlySpan<byte> blob)
    {
        if (blob.Length < 16)
        {
            return null;
        }

        var channels = BitConverter.ToUInt16(blob[2..]);
        var sampleRate = (int)BitConverter.ToUInt32(blob[4..]);
        var bits = BitConverter.ToUInt16(blob[14..]);

        // Em WAVEFORMATEXTENSIBLE o valor que importa é wValidBitsPerSample
        // (um contêiner de 32 bits pode carregar 24 bits úteis).
        var formatTag = BitConverter.ToUInt16(blob);
        if (formatTag == WaveFormatEx.WAVE_FORMAT_EXTENSIBLE && blob.Length >= 40)
        {
            var validBits = BitConverter.ToUInt16(blob[18..]);
            if (validBits > 0)
            {
                bits = validBits;
            }
        }

        return sampleRate <= 0 || channels <= 0 ? null : (sampleRate, bits, channels);
    }
}
