using System.Globalization;
using Microsoft.Win32;
using Quantum.Audio.Models;

namespace Quantum.Audio.Drivers;

/// <summary>
/// Lê os dados do driver direto do ramo PnP do registro. É o mesmo caminho que o
/// Gerenciador de Dispositivos percorre, e evita depender do pacote System.Management.
/// </summary>
public sealed class DriverService : IDriverService
{
    private const string EnumRoot = @"SYSTEM\CurrentControlSet\Enum\";
    private const string ClassRoot = @"SYSTEM\CurrentControlSet\Control\Class\";

    public AudioDriverInfo GetDriverInfo(AudioDeviceInfo device)
    {
        if (string.IsNullOrWhiteSpace(device.InstanceId))
        {
            return AudioDriverInfo.Unknown with { Connection = device.Connection };
        }

        try
        {
            using var enumKey = Registry.LocalMachine.OpenSubKey(EnumRoot + device.InstanceId);
            if (enumKey is null)
            {
                return AudioDriverInfo.Unknown with
                {
                    InstanceId = device.InstanceId,
                    Connection = device.Connection,
                };
            }

            var description = CleanResourceString(enumKey.GetValue("DeviceDesc") as string);
            var manufacturer = CleanResourceString(enumKey.GetValue("Mfg") as string);
            var service = enumKey.GetValue("Service") as string;
            var driverKeyPath = enumKey.GetValue("Driver") as string;

            using var classKey = string.IsNullOrEmpty(driverKeyPath)
                ? null
                : Registry.LocalMachine.OpenSubKey(ClassRoot + driverKeyPath);

            return new AudioDriverInfo
            {
                Description = classKey?.GetValue("DriverDesc") as string ?? description,
                Provider = classKey?.GetValue("ProviderName") as string ?? manufacturer,
                Version = classKey?.GetValue("DriverVersion") as string,
                Date = ParseDriverDate(classKey?.GetValue("DriverDate") as string),
                InfName = classKey?.GetValue("InfPath") as string,
                InfSection = classKey?.GetValue("InfSection") as string,
                Service = service,
                InstanceId = device.InstanceId,
                Connection = device.Connection,
            };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return AudioDriverInfo.Unknown with
            {
                Description = "Sem permissão para ler o registro do driver",
                InstanceId = device.InstanceId,
                Connection = device.Connection,
            };
        }
    }

    /// <summary>O registro grava datas de driver como "M-d-yyyy".</summary>
    internal static DateTime? ParseDriverDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string[] formats = ["M-d-yyyy", "MM-dd-yyyy", "M/d/yyyy", "MM/dd/yyyy"];
        return DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Valores como "@wdma_usb.inf,%usb\class_01%;USB Audio Device" carregam o texto
    /// legível depois do ponto e vírgula.
    /// </summary>
    internal static string? CleanResourceString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var separator = raw.LastIndexOf(';');
        return separator >= 0 && separator < raw.Length - 1 ? raw[(separator + 1)..].Trim() : raw.Trim();
    }
}
