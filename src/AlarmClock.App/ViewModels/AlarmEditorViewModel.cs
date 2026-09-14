using System.Globalization;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Localization;
using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlarmClock.App.ViewModels;

/// <summary>Which kind of schedule the editor is building. | Que tipo de agenda o editor está montando.</summary>
public enum ScheduleKind
{
    Once,
    Daily,
    Weekly,
    Interval,
}

/// <summary>A weekday toggle in the editor. | Um dia da semana no seletor do editor.</summary>
public sealed partial class DayToggle : ObservableObject
{
    public required WeekDays Flag { get; init; }

    public required string Label { get; init; }

    [ObservableProperty]
    private bool _isChecked;
}

/// <summary>An urgency choice in the editor. | Uma opção de urgência no editor.</summary>
public sealed partial class UrgencyChoice : ObservableObject
{
    public required UrgencyProfile Profile { get; init; }

    /// <summary>Localized level name. | Nome localizado do nível.</summary>
    public string Label => Loc.UrgencyName(Profile.Level);

    [ObservableProperty]
    private bool _isChecked;
}

/// <summary>View model of the create/edit alarm dialog. | View model do diálogo de criar/editar alarme.</summary>
public sealed partial class AlarmEditorViewModel : ObservableObject
{
    /// <summary>Idle time treated as "away" for new alarms. | Tempo ocioso tratado como "ausente" para alarmes novos.</summary>
    private static readonly TimeSpan IdleThreshold = TimeSpan.FromMinutes(5);

    /// <summary>Current-language culture. | Cultura do idioma atual.</summary>
    private static CultureInfo Culture => Loc.Culture;

    /// <summary>Date pattern of the current language (dd/MM/yyyy or MM/dd/yyyy). | Padrão de data do idioma atual (dd/MM/yyyy ou MM/dd/yyyy).</summary>
    private static string DatePattern => Loc.Get("Fmt_DateLong");

    private readonly ISystemClock _clock;
    private readonly Guid _id;

    /// <summary>Cycle anchor of the edited alarm, if it had one. | Âncora do ciclo do alarme editado, se havia.</summary>
    private readonly DateTimeOffset? _ancoraOriginal;

    /// <summary>Interval the alarm had when opened. | Intervalo que o alarme tinha ao abrir.</summary>
    private readonly TimeSpan? _intervaloOriginal;

    public AlarmEditorViewModel(ISystemClock clock, Alarm? existente = null)
    {
        _clock = clock;
        _id = existente?.Id ?? Guid.NewGuid();

        IsNew = existente is null;

        Days =
        [
            new DayToggle { Flag = WeekDays.Monday, Label = Loc.Get("Day_1") },
            new DayToggle { Flag = WeekDays.Tuesday, Label = Loc.Get("Day_2") },
            new DayToggle { Flag = WeekDays.Wednesday, Label = Loc.Get("Day_3") },
            new DayToggle { Flag = WeekDays.Thursday, Label = Loc.Get("Day_4") },
            new DayToggle { Flag = WeekDays.Friday, Label = Loc.Get("Day_5") },
            new DayToggle { Flag = WeekDays.Saturday, Label = Loc.Get("Day_6") },
            new DayToggle { Flag = WeekDays.Sunday, Label = Loc.Get("Day_0") },
        ];

        Urgencies = [.. UrgencyProfiles.All.Select(p => new UrgencyChoice { Profile = p })];

        // Default to a near-future rounded time so a one-time alarm is not born in the past. | Padrão num horário futuro arredondado para um alarme de uma vez não nascer no passado.
        var sugestao = ProximoHorarioRedondo(_clock);
        _dateText = sugestao.ToString(DatePattern, Culture);
        _timeText = sugestao.ToString("HH:mm", Culture);

        if (existente is null)
        {
            SelectUrgency(UrgencyLevel.Normal);
            Kind = ScheduleKind.Daily;
            return;
        }

        _title = existente.Title;
        _message = existente.Message ?? string.Empty;
        _customSoundPath = existente.CustomSoundPath ?? string.Empty;
        _skipWhenAway = existente.SkipIfIdleFor is not null;
        _escalate = existente.Escalation is not null;
        SelectUrgency(existente.Urgency);

        if (existente.Schedule is IntervalSchedule intervalo)
        {
            _ancoraOriginal = intervalo.Anchor;
            _intervaloOriginal = intervalo.Every;
        }

        LoadSchedule(existente.Schedule);
    }

    /// <summary>Whether this is a new alarm. | Se é um alarme novo.</summary>
    public bool IsNew { get; }

    /// <summary>Localized window title. | Título localizado da janela.</summary>
    public string WindowTitle => Loc.Get(IsNew ? "Editor_New" : "Editor_Edit");

    /// <summary>Weekday toggles. | Seletores de dia da semana.</summary>
    public IReadOnlyList<DayToggle> Days { get; }

    /// <summary>Urgency choices. | Opções de urgência.</summary>
    public IReadOnlyList<UrgencyChoice> Urgencies { get; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _customSoundPath = string.Empty;

    /// <summary>Time as "HH:mm". | Hora como "HH:mm".</summary>
    [ObservableProperty]
    private string _timeText;

    /// <summary>Date, used only for one-time alarms. | Data, usada só em alarmes de uma vez.</summary>
    [ObservableProperty]
    private string _dateText;

    /// <summary>Minutes between firings, for interval schedules. | Minutos entre disparos, para agendas por intervalo.</summary>
    [ObservableProperty]
    private string _intervalMinutesText = "45";

    /// <summary>Whether the interval is limited to a time range. | Se o intervalo é limitado a uma faixa de horário.</summary>
    [ObservableProperty]
    private bool _useWindow = true;

    [ObservableProperty]
    private string _windowFromText = "09:00";

    [ObservableProperty]
    private string _windowToText = "18:00";

    /// <summary>Don't alert while keyboard/mouse are idle. | Não alertar enquanto teclado/mouse estão parados.</summary>
    [ObservableProperty]
    private bool _skipWhenAway;

    /// <summary>Escalate when the alert is ignored. | Escalar quando o alerta é ignorado.</summary>
    [ObservableProperty]
    private bool _escalate;

    /// <summary>Selected schedule kind. | Tipo de agenda selecionado.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnce))]
    [NotifyPropertyChangedFor(nameof(IsDaily))]
    [NotifyPropertyChangedFor(nameof(IsWeekly))]
    [NotifyPropertyChangedFor(nameof(IsInterval))]
    [NotifyPropertyChangedFor(nameof(HasFixedTime))]
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

    public bool IsInterval
    {
        get => Kind == ScheduleKind.Interval;
        set { if (value) { Kind = ScheduleKind.Interval; } }
    }

    /// <summary>Interval schedules have no fixed time. | Agendas por intervalo não têm hora marcada.</summary>
    public bool HasFixedTime => Kind != ScheduleKind.Interval;

    /// <summary>Set when the user confirms; null if cancelled. | Preenchido quando o usuário confirma; nulo se cancelou.</summary>
    public Alarm? Result { get; private set; }

    /// <summary>Raised to close the dialog (true = saved). | Emitido para fechar o diálogo (true = salvo).</summary>
    public event Action<bool>? CloseRequested;

    /// <summary>Raised with a message when validation fails. | Emitido com uma mensagem quando a validação falha.</summary>
    public event Action<string>? ValidationFailed;

    /// <summary>Now plus a small buffer, rounded up to the next 5 minutes. | Agora mais uma folga, arredondado para o próximo múltiplo de 5 min.</summary>
    private static DateTime ProximoHorarioRedondo(ISystemClock clock)
    {
        var agora = TimeZoneInfo.ConvertTime(clock.Now, clock.LocalTimeZone).DateTime;

        var alvo = agora.AddSeconds(30);
        alvo = alvo.AddTicks(-(alvo.Ticks % TimeSpan.TicksPerMinute)); // drop seconds | zera segundos

        var resto = alvo.Minute % 5;
        if (resto != 0)
        {
            alvo = alvo.AddMinutes(5 - resto);
        }
        else if (alvo <= agora)
        {
            alvo = alvo.AddMinutes(5);
        }

        return alvo;
    }

    /// <summary>Validates and, on success, produces the alarm and closes. | Valida e, se ok, produz o alarme e fecha.</summary>
    [RelayCommand]
    private void Save()
    {
        if (!TryBuild(out var alarme, out var erro))
        {
            ValidationFailed?.Invoke(erro ?? Loc.Get("Val_SaveFailed"));
            return;
        }

        Result = alarme;
        CloseRequested?.Invoke(true);
    }

    /// <summary>Cancels without saving. | Cancela sem salvar.</summary>
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    /// <summary>Opens a file dialog to pick a custom sound. | Abre um diálogo para escolher um som próprio.</summary>
    [RelayCommand]
    private void BrowseSound()
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.Get("Editor_SoundDialogTitle"),
            Filter = Loc.Get("Editor_SoundFilter"),
            CheckFileExists = true,
        };

        if (dialogo.ShowDialog() == true)
        {
            CustomSoundPath = dialogo.FileName;
        }
    }

    /// <summary>Clears the custom sound. | Limpa o som próprio.</summary>
    [RelayCommand]
    private void ClearSound() => CustomSoundPath = string.Empty;

    /// <summary>Validates the fields and builds the alarm, or returns an error. | Valida os campos e monta o alarme, ou retorna um erro.</summary>
    private bool TryBuild(out Alarm? alarme, out string? erro)
    {
        alarme = null;
        erro = null;

        if (string.IsNullOrWhiteSpace(Title))
        {
            erro = Loc.Get("Val_NeedName");
            return false;
        }

        var hora = default(TimeOnly);

        if (HasFixedTime && !TimeOnly.TryParseExact(TimeText.Trim(), "HH\\:mm", Culture, DateTimeStyles.None, out hora))
        {
            erro = Loc.Get("Val_BadTime");
            return false;
        }

        ISchedule agenda;

        switch (Kind)
        {
            case ScheduleKind.Interval:
                if (!int.TryParse(IntervalMinutesText.Trim(), out var minutos) || minutos < 1)
                {
                    erro = Loc.Get("Val_BadInterval");
                    return false;
                }

                TimeOnly? de = null;
                TimeOnly? ate = null;

                if (UseWindow)
                {
                    if (!TimeOnly.TryParseExact(WindowFromText.Trim(), "HH\\:mm", Culture, DateTimeStyles.None, out var inicio) ||
                        !TimeOnly.TryParseExact(WindowToText.Trim(), "HH\\:mm", Culture, DateTimeStyles.None, out var fim))
                    {
                        erro = Loc.Get("Val_BadWindow");
                        return false;
                    }

                    if (inicio == fim)
                    {
                        erro = Loc.Get("Val_WindowEqual");
                        return false;
                    }

                    de = inicio;
                    ate = fim;
                }

                // Keep the running cycle's anchor unless the interval changed. | Mantém a âncora do ciclo em curso, a menos que o intervalo mude.
                var novoIntervalo = TimeSpan.FromMinutes(minutos);
                var ancora = novoIntervalo == _intervaloOriginal && _ancoraOriginal is not null
                    ? _ancoraOriginal.Value
                    : _clock.Now;

                agenda = new IntervalSchedule(novoIntervalo, ancora, de, ate);
                break;

            case ScheduleKind.Once:
                if (!DateOnly.TryParseExact(DateText.Trim(), DatePattern, Culture, DateTimeStyles.None, out var data))
                {
                    erro = Loc.Format("Val_BadDate", DatePattern);
                    return false;
                }

                var quando = new DateTimeOffset(
                    data.ToDateTime(hora),
                    _clock.LocalTimeZone.GetUtcOffset(data.ToDateTime(hora)));

                if (quando <= _clock.Now)
                {
                    erro = Loc.Get("Val_PastInstant");
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
                    erro = Loc.Get("Val_NeedWeekday");
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
            SkipIfIdleFor = SkipWhenAway ? IdleThreshold : null,
            Escalation = Escalate ? EscalationPolicy.Default : null,
        };

        return true;
    }

    /// <summary>Checks the urgency choice matching the level. | Marca a opção de urgência correspondente ao nível.</summary>
    private void SelectUrgency(UrgencyLevel level)
    {
        foreach (var opcao in Urgencies)
        {
            opcao.IsChecked = opcao.Profile.Level == level;
        }
    }

    /// <summary>Fills the fields from an existing schedule. | Preenche os campos a partir de uma agenda existente.</summary>
    private void LoadSchedule(ISchedule agenda)
    {
        switch (agenda)
        {
            case OneTimeSchedule once:
                Kind = ScheduleKind.Once;
                var local = TimeZoneInfo.ConvertTime(once.At, _clock.LocalTimeZone);
                DateText = local.ToString(DatePattern, Culture);
                TimeText = local.ToString("HH:mm", Culture);
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

            case IntervalSchedule intervalo:
                Kind = ScheduleKind.Interval;
                IntervalMinutesText = ((int)intervalo.Every.TotalMinutes).ToString(Culture);
                UseWindow = intervalo.HasWindow;

                if (intervalo.HasWindow)
                {
                    WindowFromText = intervalo.ActiveFrom!.Value.ToString("HH\\:mm");
                    WindowToText = intervalo.ActiveTo!.Value.ToString("HH\\:mm");
                }

                break;
        }
    }
}
