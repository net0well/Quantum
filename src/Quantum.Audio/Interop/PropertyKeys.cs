namespace Quantum.Audio.Interop;

/// <summary>
/// Chaves de propriedade (PROPERTYKEY) usadas nos property stores de endpoint de áudio.
/// Os GUIDs vêm de mmdeviceapi.h / functiondiscoverykeys_devpkey.h / devpkey.h.
/// </summary>
internal static class PropertyKeys
{
    // functiondiscoverykeys_devpkey.h
    /// <summary>Nome amigável completo: "Fone de ouvido do headset (HyperX Virtual Surround Sound)".</summary>
    public static readonly PropertyKey DeviceFriendlyName = new("a45c254e-df1c-4efd-8020-67d146a850e0", 14);

    /// <summary>Nome curto atribuído pelo usuário: "Fone de ouvido do headset".</summary>
    public static readonly PropertyKey DeviceDescription = new("a45c254e-df1c-4efd-8020-67d146a850e0", 2);

    /// <summary>Nome da interface/adaptador: "HyperX Virtual Surround Sound".</summary>
    public static readonly PropertyKey DeviceInterfaceFriendlyName = new("b3f8fa53-0004-438e-9003-51a46e139bfc", 6);

    /// <summary>Caminho da instância PnP do dispositivo físico por trás do endpoint.</summary>
    public static readonly PropertyKey DeviceInstanceId = new("b3f8fa53-0004-438e-9003-51a46e139bfc", 2);

    /// <summary>Nome do enumerador PnP: "USB", "HDAUDIO".</summary>
    public static readonly PropertyKey DeviceEnumeratorName = new("a45c254e-df1c-4efd-8020-67d146a850e0", 24);

    /// <summary>Índice do ícone do endpoint (mmres.dll).</summary>
    public static readonly PropertyKey DeviceIconPath = new("259abffc-50a7-47ce-af08-68c9a7d73366", 12);

    /// <summary>Tipo de forma física do conector (fone, alto-falante, digital...).</summary>
    public static readonly PropertyKey DeviceFormFactor = new("1da5d803-d492-4edd-8c23-e0c0ffee7f0e", 0);

    // devpkey.h — presentes no endpoint para o driver que o expõe
    public static readonly PropertyKey DriverInfPath = new("a8b865dd-2e3d-4094-ad97-e593a70c75d6", 5);
    public static readonly PropertyKey DriverInfSection = new("a8b865dd-2e3d-4094-ad97-e593a70c75d6", 6);
    public static readonly PropertyKey DriverMatchingDeviceId = new("a8b865dd-2e3d-4094-ad97-e593a70c75d6", 8);

    // mmdeviceapi.h — formato de mixagem do endpoint em modo compartilhado.
    // É exatamente o que a caixa "Formato padrão" do Windows lê e grava.
    public static readonly PropertyKey AudioEngineDeviceFormat = new("f19f064d-082c-4e27-bc73-6882a1bb8e4c", 0);
    public static readonly PropertyKey AudioEngineOemFormat = new("e4870e26-3cc5-4cd2-ba46-ca0a9a70ed04", 0);

    // Áudio espacial. Não documentado publicamente — os valores abaixo foram
    // confirmados comparando todos os endpoints da máquina:
    //   SpatialCurrentFormat  -> idêntico em todos os endpoints quando desligado (0)
    //   SpatialFormatCount    -> contagem de formatos registrados (não é a seleção)
    //   SpatialFormatCatalog  -> um blob por formato, a partir do pid 2
    public static readonly PropertyKey SpatialCurrentFormat = new("6737016f-5360-48ee-af05-e616c8ff27a7", 2);
    public static readonly PropertyKey SpatialFormatCount = new("913bc9a7-624b-4a30-96ac-5064a9fc6589", 2);
    public static readonly Guid SpatialFormatCatalog = new("a45429a4-aa63-4480-b7f8-3f2552daee93");

    /// <summary>Primeiro pid do catálogo de formatos espaciais.</summary>
    public const int SpatialCatalogFirstPid = 2;
}
