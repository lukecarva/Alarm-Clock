using System.Text;
using System.Text.Json;
using AlarmClock.Core.Scheduling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlarmClock.Core.Persistence;

/// <summary>Loads and saves the scheduler's runtime state (snoozes and escalations). | Carrega e salva o estado de execução do agendador (adiamentos e escaladas).</summary>
public interface ISchedulerStateStore
{
    /// <summary>Reads the saved state, or an empty state when there is none. | Lê o estado salvo, ou um estado vazio quando não há.</summary>
    SchedulerState Load();

    /// <summary>Writes the current state. | Grava o estado atual.</summary>
    void Save(SchedulerState state);
}

/// <summary>Stores the scheduler state in a JSON file next to the alarms. | Guarda o estado do agendador num arquivo JSON ao lado dos alarmes.</summary>
public sealed class JsonSchedulerStateStore : ISchedulerStateStore
{
    private readonly string _path;
    private readonly ILogger<JsonSchedulerStateStore> _log;

    public JsonSchedulerStateStore(string path, ILogger<JsonSchedulerStateStore>? log = null)
    {
        _path = path;
        _log = log ?? NullLogger<JsonSchedulerStateStore>.Instance;
    }

    /// <summary>Reads the state; quarantines an unreadable file and returns empty. | Lê o estado; põe em quarentena um arquivo ilegível e retorna vazio.</summary>
    public SchedulerState Load()
    {
        if (!File.Exists(_path))
        {
            return new SchedulerState();
        }

        try
        {
            var json = File.ReadAllText(_path, Encoding.UTF8);
            return JsonSerializer.Deserialize<SchedulerState>(json, JsonAlarmStore.SerializerOptions)
                ?? new SchedulerState();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Move the unreadable file aside instead of overwriting it. | Move o arquivo ilegível para o lado em vez de sobrescrevê-lo.
            var quarentena = $"{_path}.corrompido-{DateTime.Now:yyyyMMdd-HHmmss}";

            _log.LogError(
                ex,
                "scheduler-state.json ilegível. Movendo para {Quarentena} e começando vazio.",
                quarentena);

            try
            {
                File.Move(_path, quarentena);
            }
            catch (IOException moveEx)
            {
                _log.LogError(moveEx, "Não deu para mover o arquivo de estado corrompido.");
            }

            return new SchedulerState();
        }
    }

    /// <summary>Writes the state atomically (temp file + replace with backup). | Grava o estado de forma atômica (arquivo temporário + replace com backup).</summary>
    public void Save(SchedulerState state)
    {
        var json = JsonSerializer.Serialize(state, JsonAlarmStore.SerializerOptions);

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

        _log.LogDebug(
            "Estado do agendador salvo: {Adiamentos} adiamento(s), {Escaladas} escalada(s).",
            state.Snoozes.Count,
            state.Escalations.Count);
    }
}
