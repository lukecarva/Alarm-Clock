using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlarmClock.Core.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlarmClock.Core.Persistence;

/// <summary>Loads and saves the alarm list. | Carrega e salva a lista de alarmes.</summary>
public interface IAlarmStore
{
    /// <summary>Reads all alarms. | Lê todos os alarmes.</summary>
    IReadOnlyList<Alarm> Load();

    /// <summary>Writes all alarms. | Grava todos os alarmes.</summary>
    void Save(IEnumerable<Alarm> alarms);
}

/// <summary>Stores alarms in a human-readable JSON file. | Guarda os alarmes num arquivo JSON legível.</summary>
public sealed class JsonAlarmStore : IAlarmStore
{
    /// <summary>Serializer options: indented, no nulls, unescaped accents, enums as names. | Opções do serializador: indentado, sem nulos, acentos sem escape, enums como nome.</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly ILogger<JsonAlarmStore> _log;

    public JsonAlarmStore(string path, ILogger<JsonAlarmStore>? log = null)
    {
        _path = path;
        _log = log ?? NullLogger<JsonAlarmStore>.Instance;
    }

    /// <summary>Reads the alarms; quarantines an unreadable file and returns empty. | Lê os alarmes; põe em quarentena um arquivo ilegível e retorna vazio.</summary>
    public IReadOnlyList<Alarm> Load()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_path, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<Alarm>>(json, SerializerOptions) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Move the unreadable file aside instead of overwriting it. | Move o arquivo ilegível para o lado em vez de sobrescrevê-lo.
            var quarentena = $"{_path}.corrompido-{DateTime.Now:yyyyMMdd-HHmmss}";

            _log.LogError(
                ex,
                "alarms.json ilegível. Movendo para {Quarentena} e começando vazio.",
                quarentena);

            try
            {
                File.Move(_path, quarentena);
            }
            catch (IOException moveEx)
            {
                _log.LogError(moveEx, "Não deu para mover o arquivo corrompido.");
            }

            return [];
        }
    }

    /// <summary>Writes the alarms atomically (temp file + replace with backup). | Grava os alarmes de forma atômica (arquivo temporário + replace com backup).</summary>
    public void Save(IEnumerable<Alarm> alarms)
    {
        var lista = alarms.ToList();
        var json = JsonSerializer.Serialize(lista, SerializerOptions);

        var pasta = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(pasta))
        {
            Directory.CreateDirectory(pasta);
        }

        var temp = _path + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (File.Exists(_path))
        {
            File.Replace(temp, _path, _path + ".bak", ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, _path);
        }

        _log.LogDebug("{Quantidade} alarme(s) salvos em {Caminho}.", lista.Count, _path);
    }
}
