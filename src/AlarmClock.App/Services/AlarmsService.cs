using AlarmClock.Core.Model;
using AlarmClock.Core.Persistence;
using Microsoft.Extensions.Logging;

namespace AlarmClock.App.Services;

/// <summary>
/// Dona da lista de alarmes. Toda alteração persiste na hora e avisa quem
/// depende — não existe "salvar" manual para o usuário esquecer de clicar.
/// </summary>
public sealed class AlarmsService(IAlarmStore store, ILogger<AlarmsService> log)
{
    private readonly IAlarmStore _store = store;
    private readonly ILogger<AlarmsService> _log = log;

    private List<Alarm> _items = [];

    public IReadOnlyList<Alarm> Items => _items;

    /// <summary>Disparado depois de qualquer alteração já persistida.</summary>
    public event EventHandler? Changed;

    public void Load()
    {
        _items = [.. _store.Load()];
        _log.LogInformation("{Quantidade} alarme(s) carregados.", _items.Count);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Alarm? Find(Guid id) => _items.FirstOrDefault(a => a.Id == id);

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

    public void SetEnabled(Guid id, bool enabled)
    {
        var alarm = Find(id);
        if (alarm is null || alarm.IsEnabled == enabled)
        {
            return;
        }

        AddOrUpdate(alarm with { IsEnabled = enabled });
    }

    private void Persist()
    {
        _store.Save(_items);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
