using System.Globalization;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Localization;
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
    Interval,
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

    public string Label => Loc.UrgencyName(Profile.Level);

    [ObservableProperty]
    private bool _isChecked;
}

public sealed partial class AlarmEditorViewModel : ObservableObject
{
    /// <summary>
    /// Quanto tempo de teclado e mouse parados já conta como "não estou aqui".
    /// </summary>
    private static readonly TimeSpan IdleThreshold = TimeSpan.FromMinutes(5);

    private static CultureInfo Culture => Loc.Culture;

    /// <summary>Padrão de data do idioma atual (dd/MM/yyyy ou MM/dd/yyyy).</summary>
    private static string DatePattern => Loc.Get("Fmt_DateLong");

    private readonly ISystemClock _clock;
    private readonly Guid _id;

    /// <summary>Âncora do ciclo do alarme sendo editado, quando havia uma.</summary>
    private readonly DateTimeOffset? _ancoraOriginal;

    /// <summary>Intervalo que o alarme tinha ao ser aberto para edição.</summary>
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

        // Padrão no futuro próximo, não um "07:00" fixo: assim um alarme de uma
        // vez já nasce depois de agora, em vez de cair na validação de "já
        // passou". Arredondado para o próximo múltiplo de 5 min por estética.
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

    public bool IsNew { get; }

    public string WindowTitle => Loc.Get(IsNew ? "Editor_New" : "Editor_Edit");

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

    /// <summary>Minutos entre disparos, quando a agenda é por intervalo.</summary>
    [ObservableProperty]
    private string _intervalMinutesText = "45";

    [ObservableProperty]
    private bool _useWindow = true;

    [ObservableProperty]
    private string _windowFromText = "09:00";

    [ObservableProperty]
    private string _windowToText = "18:00";

    /// <summary>Não alertar se o teclado e o mouse estiverem parados.</summary>
    [ObservableProperty]
    private bool _skipWhenAway;

    /// <summary>Subir de nível quando o alerta é ignorado.</summary>
    [ObservableProperty]
    private bool _escalate;

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

    /// <summary>Agenda por intervalo não tem hora marcada — tem ritmo.</summary>
    public bool HasFixedTime => Kind != ScheduleKind.Interval;

    /// <summary>Preenchido quando o usuário confirma; nulo se cancelou.</summary>
    public Alarm? Result { get; private set; }

    public event Action<bool>? CloseRequested;

    /// <summary>
    /// Disparado quando o alarme não passa na validação. A janela mostra num
    /// popup — o texto no rodapé passava despercebido, ainda mais com o
    /// formulário rolado.
    /// </summary>
    public event Action<string>? ValidationFailed;

    /// <summary>
    /// Agora, empurrado alguns segundos para a frente e arredondado para cima ao
    /// próximo múltiplo de 5 min. O empurrão evita nascer no passado se o minuto
    /// já estava fechado; o arredondamento cruza a meia-noite pela própria data.
    /// </summary>
    private static DateTime ProximoHorarioRedondo(ISystemClock clock)
    {
        var agora = TimeZoneInfo.ConvertTime(clock.Now, clock.LocalTimeZone).DateTime;

        var alvo = agora.AddSeconds(30);
        alvo = alvo.AddTicks(-(alvo.Ticks % TimeSpan.TicksPerMinute)); // zera segundos

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

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

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

    [RelayCommand]
    private void ClearSound() => CustomSoundPath = string.Empty;

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

                // Mexer no intervalo reinicia a contagem; mexer só no título ou
                // na urgência preserva o ritmo que já estava correndo.
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
            SkipIfIdleFor = SkipWhenAway ? IdleThreshold : null,
            Escalation = Escalate ? EscalationPolicy.Default : null,
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
