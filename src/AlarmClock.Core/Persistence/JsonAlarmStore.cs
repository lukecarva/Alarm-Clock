using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlarmClock.Core.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlarmClock.Core.Persistence;

public interface IAlarmStore
{
    IReadOnlyList<Alarm> Load();

    void Save(IEnumerable<Alarm> alarms);
}

/// <summary>
/// Um arquivo JSON legível e editável à mão. Para uso pessoal, com dezenas de
/// alarmes, um banco seria peso morto e uma migração a mais para manter.
/// </summary>
public sealed class JsonAlarmStore : IAlarmStore
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Sem escapar acentos: o arquivo é para ser lido por gente.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _path;
    private readonly ILogger<JsonAlarmStore> _log;

    public JsonAlarmStore(string path, ILogger<JsonAlarmStore>? log = null)
    {
        _path = path;
        _log = log ?? NullLogger<JsonAlarmStore>.Instance;
    }

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
            // Nunca sobrescrever silenciosamente um arquivo que não deu para
            // ler: pode ser a única cópia dos alarmes. Guarda de lado e segue
            // com a lista vazia.
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

    public void Save(IEnumerable<Alarm> alarms)
    {
        var lista = alarms.ToList();
        var json = JsonSerializer.Serialize(lista, SerializerOptions);

        var pasta = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(pasta))
        {
            Directory.CreateDirectory(pasta);
        }

        // Escrita atômica: um crash no meio do Save não pode deixar o arquivo
        // truncado, porque isso apagaria todos os alarmes de uma vez.
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
