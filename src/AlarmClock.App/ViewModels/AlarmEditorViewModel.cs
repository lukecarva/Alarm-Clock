using System.Globalization;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlarmClock.App.ViewModels;

public enum ScheduleKind
{
    Once,
    Daily,
    Weekly,
}

/// <summary>Um dia da semana no seletor do editor.</summary>
public sealed partial class DayToggle : ObservableObject
{
    public required WeekDays Flag { get; init; }

    public required string Label { get; init; }

    [ObservableProperty]
    private bool _isChecked;
}

/// <summary>Uma opção de urgência no seletor do editor.</summary>
public sealed partial class UrgencyChoice : ObservableObject
{
    public required UrgencyProfile Profile { get; init; }

    public string Label => Profile.DisplayName;

    [ObservableProperty]
    private bool _isChecked;
}

public sealed partial class AlarmEditorViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly ISystemClock _clock;
    private readonly Guid _id;

    public AlarmEditorViewModel(ISystemClock clock, Alarm? existente = null)
    {
        _clock = clock;
        _id = existente?.Id ?? Guid.NewGuid();

        IsNew = existente is null;

        Days =
        [
            new DayToggle { Flag = WeekDays.Monday, Label = "seg" },
            new DayToggle { Flag = WeekDays.Tuesday, Label = "ter" },
            new DayToggle { Flag = WeekDays.Wednesday, Label = "qua" },
            new DayToggle { Flag = WeekDays.Thursday, Label = "qui" },
            new DayToggle { Flag = WeekDays.Friday, Label = "sex" },
            new DayToggle { Flag = WeekDays.Saturday, Label = "sáb" },
            new DayToggle { Flag = WeekDays.Sunday, Label = "dom" },
        ];

        Urgencies = [.. UrgencyProfiles.All.Select(p => new UrgencyChoice { Profile = p })];

        var agora = _clock.Now;
        _dateText = agora.ToString("dd/MM/yyyy", PtBr);
        _timeText = "07:00";

        if (existente is null)
        {
            SelectUrgency(UrgencyLevel.Normal);
            Kind = ScheduleKind.Daily;
            return;
        }

        _title = existente.Title;
        _message = existente.Message ?? string.Empty;
        _customSoundPath = existente.CustomSoundPath ?? string.Empty;
        SelectUrgency(existente.Urgency);
        LoadSchedule(existente.Schedule);
    }

    public bool IsNew { get; }

    public string WindowTitle => IsNew ? "Novo alarme" : "Editar alarme";

    public IReadOnlyList<DayToggle> Days { get; }

    public IReadOnlyList<UrgencyChoice> Urgencies { get; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _customSoundPath = string.Empty;

    /// <summary>"HH:mm". Texto simples em vez de um seletor de hora templatizado.</summary>
    [ObservableProperty]
    private string _timeText;

    /// <summary>"dd/MM/yyyy", só usado quando a agenda é de uma vez só.</summary>
    [ObservableProperty]
    private string _dateText;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnce))]
    [NotifyPropertyChangedFor(nameof(IsDaily))]
    [NotifyPropertyChangedFor(nameof(IsWeekly))]
    private ScheduleKind _kind = ScheduleKind.Daily;

    public bool IsOnce
    {
        get => Kind == ScheduleKind.Once;
        set { if (value) { Kind = ScheduleKind.Once; } }
    }

    public bool IsDaily
    {
        get => Kind == ScheduleKind.Daily;
        set { if (value) { Kind = ScheduleKind.Daily; } }
    }

    public bool IsWeekly
    {
        get => Kind == ScheduleKind.Weekly;
        set { if (value) { Kind = ScheduleKind.Weekly; } }
    }

    /// <summary>Preenchido quando o usuário confirma; nulo se cancelou.</summary>
    public Alarm? Result { get; private set; }

    public event Action<bool>? CloseRequested;

    [RelayCommand]
    private void Save()
    {
        if (!TryBuild(out var alarme, out var erro))
        {
            ValidationError = erro;
            return;
        }

        Result = alarme;
        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    [RelayCommand]
    private void BrowseSound()
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Som do alarme",
            Filter = "Áudio (*.wav;*.mp3;*.m4a;*.wma)|*.wav;*.mp3;*.m4a;*.wma|Todos os arquivos|*.*",
            CheckFileExists = true,
        };

        if (dialogo.ShowDialog() == true)
        {
            CustomSoundPath = dialogo.FileName;
        }
    }

    [RelayCommand]
    private void ClearSound() => CustomSoundPath = string.Empty;

    private bool TryBuild(out Alarm? alarme, out string? erro)
    {
        alarme = null;
        erro = null;

        if (string.IsNullOrWhiteSpace(Title))
        {
            erro = "Dê um nome ao alarme.";
            return false;
        }

        if (!TimeOnly.TryParseExact(TimeText.Trim(), "HH\\:mm", PtBr, DateTimeStyles.None, out var hora))
        {
            erro = "Horário inválido. Use HH:mm, por exemplo 07:30.";
            return false;
        }

        ISchedule agenda;

        switch (Kind)
        {
            case ScheduleKind.Once:
                if (!DateOnly.TryParseExact(DateText.Trim(), "dd/MM/yyyy", PtBr, DateTimeStyles.None, out var data))
                {
                    erro = "Data inválida. Use dd/MM/aaaa.";
                    return false;
                }

                var quando = new DateTimeOffset(
                    data.ToDateTime(hora),
                    _clock.LocalTimeZone.GetUtcOffset(data.ToDateTime(hora)));

                if (quando <= _clock.Now)
                {
                    erro = "Esse instante já passou.";
                    return false;
                }

                agenda = new OneTimeSchedule(quando);
                break;

            case ScheduleKind.Weekly:
                var dias = Days
                    .Where(d => d.IsChecked)
                    .Aggregate(WeekDays.None, (acumulado, d) => acumulado | d.Flag);

                if (dias == WeekDays.None)
                {
                    erro = "Escolha pelo menos um dia da semana.";
                    return false;
                }

                agenda = new WeeklySchedule(dias, hora);
                break;

            default:
                agenda = new DailySchedule(hora);
                break;
        }

        var urgencia = Urgencies.FirstOrDefault(u => u.IsChecked)?.Profile.Level ?? UrgencyLevel.Normal;

        alarme = new Alarm
        {
            Id = _id,
            Title = Title.Trim(),
            Message = string.IsNullOrWhiteSpace(Message) ? null : Message.Trim(),
            Schedule = agenda,
            Urgency = urgencia,
            CustomSoundPath = string.IsNullOrWhiteSpace(CustomSoundPath) ? null : CustomSoundPath.Trim(),
        };

        return true;
    }

    private void SelectUrgency(UrgencyLevel level)
    {
        foreach (var opcao in Urgencies)
        {
            opcao.IsChecked = opcao.Profile.Level == level;
        }
    }

    private void LoadSchedule(ISchedule agenda)
    {
        switch (agenda)
        {
            case OneTimeSchedule once:
                Kind = ScheduleKind.Once;
                var local = TimeZoneInfo.ConvertTime(once.At, _clock.LocalTimeZone);
                DateText = local.ToString("dd/MM/yyyy", PtBr);
                TimeText = local.ToString("HH:mm", PtBr);
                break;

            case WeeklySchedule weekly:
                Kind = ScheduleKind.Weekly;
                TimeText = weekly.At.ToString("HH\\:mm");
                foreach (var dia in Days)
                {
                    dia.IsChecked = (weekly.Days & dia.Flag) != 0;
                }

                break;

            case DailySchedule daily:
                Kind = ScheduleKind.Daily;
                TimeText = daily.At.ToString("HH\\:mm");
                break;
        }
    }
}
