using AlarmClock.Core.Model;
using AlarmClock.Core.Persistence;
using Microsoft.Extensions.Logging;

namespace AlarmClock.App.Services;

/// <summary>Owns the alarm list; every change persists at once and notifies. | Dona da lista de alarmes; toda alteração persiste na hora e avisa.</summary>
public sealed class AlarmsService(IAlarmStore store, ILogger<AlarmsService> log)
{
    private readonly IAlarmStore _store = store;
    private readonly ILogger<AlarmsService> _log = log;

    private List<Alarm> _items = [];

    /// <summary>The current alarms. | Os alarmes atuais.</summary>
    public IReadOnlyList<Alarm> Items => _items;

    /// <summary>Raised after any persisted change. | Emitido após qualquer alteração já persistida.</summary>
    public event EventHandler? Changed;

    /// <summary>Loads alarms from the store. | Carrega os alarmes do armazenamento.</summary>
    public void Load()
    {
        _items = [.. _store.Load()];
        _log.LogInformation("{Quantidade} alarme(s) carregados.", _items.Count);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Finds an alarm by id. | Encontra um alarme por id.</summary>
    public Alarm? Find(Guid id) => _items.FirstOrDefault(a => a.Id == id);

    /// <summary>Adds a new alarm or replaces one with the same id. | Adiciona um alarme novo ou substitui um de mesmo id.</summary>
    public void AddOrUpdate(Alarm alarm)
    {
        var indice = _items.FindIndex(a => a.Id == alarm.Id);

        if (indice >= 0)
        {
            _items[indice] = alarm;
            _log.LogInformation("Alarme {Titulo} atualizado.", alarm.Title);
        }
        else
        {
            _items.Add(alarm);
            _log.LogInformation("Alarme {Titulo} criado ({Agenda}).", alarm.Title, alarm.Schedule.Describe());
        }

        Persist();
    }

    /// <summary>Removes an alarm by id. | Remove um alarme por id.</summary>
    public void Remove(Guid id)
    {
        var alarm = Find(id);
        if (alarm is null)
        {
            return;
        }

        _items.RemoveAll(a => a.Id == id);
        _log.LogInformation("Alarme {Titulo} removido.", alarm.Title);
        Persist();
    }

    /// <summary>Enables or disables an alarm. | Liga ou desliga um alarme.</summary>
    public void SetEnabled(Guid id, bool enabled)
    {
        var alarm = Find(id);
        if (alarm is null || alarm.IsEnabled == enabled)
        {
            return;
        }

        AddOrUpdate(alarm with { IsEnabled = enabled });
    }

    /// <summary>Saves to the store and notifies listeners. | Salva no armazenamento e avisa os ouvintes.</summary>
    private void Persist()
    {
        _store.Save(_items);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
