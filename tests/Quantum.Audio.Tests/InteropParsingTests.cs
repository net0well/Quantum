using System.Text;
using Quantum.Audio.Devices;
using Quantum.Audio.Drivers;
using Quantum.Audio.Interop;
using Quantum.Audio.Models;
using Quantum.Audio.Spatial;
using Xunit;

namespace Quantum.Audio.Tests;

public class InteropParsingTests
{
    /// <summary>
    /// Monta uma entrada do catálogo espacial no mesmo formato que o Windows grava:
    /// nome no início e, contando do fim, o id a 34 bytes e a disponibilidade a 50.
    /// </summary>
    private static byte[] BuildCatalogEntry(string name, uint id, bool available)
    {
        var blob = new byte[120];
        Encoding.Unicode.GetBytes(name).CopyTo(blob, 0);
        BitConverter.GetBytes(available ? 1u : 0u).CopyTo(blob, blob.Length - 50);
        BitConverter.GetBytes(id).CopyTo(blob, blob.Length - 34);
        return blob;
    }

    [Fact]
    public void Catalogo_espacial_le_nome_id_e_disponibilidade()
    {
        var entry = SpatialAudioService.ParseCatalogEntry(
            BuildCatalogEntry("Windows Sonic para Fones de Ouvido", 1, available: true));

        Assert.NotNull(entry);
        Assert.Equal(1u, entry.Id);
        Assert.Equal("Windows Sonic para Fones de Ouvido", entry.Name);
        Assert.True(entry.IsAvailable);
        Assert.Null(entry.Requirement);
    }

    [Fact]
    public void Formato_espacial_sem_app_instalado_vira_indisponivel()
    {
        var entry = SpatialAudioService.ParseCatalogEntry(
            BuildCatalogEntry("Dolby Atmos for Headphones", 3, available: false));

        Assert.NotNull(entry);
        Assert.False(entry.IsAvailable);
        Assert.NotNull(entry.Requirement);
    }

    [Fact]
    public void Blob_curto_demais_nao_derruba_o_parser()
    {
        Assert.Null(SpatialAudioService.ParseCatalogEntry(new byte[10]));
    }

    [Fact]
    public void Blob_sem_nome_e_descartado()
    {
        Assert.Null(SpatialAudioService.ParseCatalogEntry(BuildCatalogEntry(string.Empty, 5, true)));
    }

    [Fact]
    public void WaveFormat_extensible_usa_os_bits_validos_e_nao_o_conteiner()
    {
        // 32 bits de contêiner carregando 24 bits úteis, 48 kHz, estéreo.
        var format = WaveFormatExtensible.CreatePcm(48000, 24, 2);
        var bytes = new byte[System.Runtime.InteropServices.Marshal.SizeOf<WaveFormatExtensible>()];
        var handle = System.Runtime.InteropServices.Marshal.AllocHGlobal(bytes.Length);

        try
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(format, handle, false);
            System.Runtime.InteropServices.Marshal.Copy(handle, bytes, 0, bytes.Length);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(handle);
        }

        var parsed = WaveFormatExtensible.Parse(bytes);

        Assert.NotNull(parsed);
        Assert.Equal(48000, parsed.Value.SampleRate);
        Assert.Equal(24, parsed.Value.BitsPerSample);
        Assert.Equal(2, parsed.Value.Channels);
    }

    [Fact]
    public void WaveFormatEx_tem_exatamente_18_bytes()
    {
        // Se ganhar padding, toda leitura de formato do registro sai errada.
        Assert.Equal(18, System.Runtime.InteropServices.Marshal.SizeOf<WaveFormatEx>());
        Assert.Equal(40, System.Runtime.InteropServices.Marshal.SizeOf<WaveFormatExtensible>());
    }

    [Fact]
    public void PropVariant_tem_o_tamanho_da_struct_nativa()
    {
        // 24 bytes em x64. Menor que isso, a chamada COM corrompe a pilha.
        var expected = Environment.Is64BitProcess ? 24 : 16;
        Assert.Equal(expected, System.Runtime.InteropServices.Marshal.SizeOf<PropVariant>());
    }

    [Theory]
    [InlineData("{1}.USB\\VID_03F0&PID_0B92&MI_00\\6&2473ED31&0&0000", "USB\\VID_03F0&PID_0B92&MI_00\\6&2473ED31&0&0000")]
    [InlineData("USB\\VID_03F0", "USB\\VID_03F0")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Prefixo_do_caminho_de_instancia_e_removido(string? raw, string? expected)
    {
        Assert.Equal(expected, AudioDeviceCatalog.NormalizeInstanceId(raw));
    }

    [Theory]
    [InlineData(0x1, AudioDeviceState.Active)]
    [InlineData(0x2, AudioDeviceState.Disabled)]
    [InlineData(0x4, AudioDeviceState.NotPresent)]
    [InlineData(0x8, AudioDeviceState.Unplugged)]
    public void Estado_do_endpoint_e_mapeado(int raw, AudioDeviceState expected)
    {
        Assert.Equal(expected, AudioDeviceCatalog.MapState(raw));
    }

    [Theory]
    [InlineData("@wdma_usb.inf,%usb\\class_01%;USB Audio Device", "USB Audio Device")]
    [InlineData("Microsoft", "Microsoft")]
    [InlineData(null, null)]
    public void Strings_de_recurso_do_registro_ficam_legiveis(string? raw, string? expected)
    {
        Assert.Equal(expected, DriverService.CleanResourceString(raw));
    }

    [Theory]
    [InlineData("7-21-2026", 2026, 7, 21)]
    [InlineData("11-04-2025", 2025, 11, 4)]
    public void Data_do_driver_e_lida_no_formato_do_registro(string raw, int year, int month, int day)
    {
        var parsed = DriverService.ParseDriverDate(raw);

        Assert.NotNull(parsed);
        Assert.Equal(new DateTime(year, month, day), parsed.Value.Date);
    }

    [Fact]
    public void Data_invalida_devolve_nulo()
    {
        Assert.Null(DriverService.ParseDriverDate("nao e data"));
        Assert.Null(DriverService.ParseDriverDate(null));
    }
}
