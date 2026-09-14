using AlarmClock.App.ViewModels;
using AlarmClock.Core.Localization;
using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;

namespace AlarmClock.App.Tests;

public class AlarmEditorViewModelTests
{
    // Fixed English-language clock at 2026-06-10 09:00 UTC. | Relógio fixo em inglês, 10/06/2026 09:00 UTC.
    private static FixedClock Clock() => new(new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero));

    private static AlarmEditorViewModel New(FixedClock clock, Alarm? existing = null)
    {
        // Tests assert on structure/behavior, not on message language. | Os testes verificam estrutura/comportamento, não o idioma das mensagens.
        Loc.Set(AppLanguage.English);
        return new AlarmEditorViewModel(clock, existing);
    }

    /// <summary>Runs Save and returns the built alarm, or a validation error. | Roda Save e retorna o alarme montado, ou o erro de validação.</summary>
    private static (Alarm? Result, string? Error) Save(AlarmEditorViewModel vm)
    {
        string? erro = null;
        vm.ValidationFailed += m => erro = m;
        vm.SaveCommand.Execute(null);
        return (vm.Result, erro);
    }

    [Fact]
    public void New_alarm_defaults_to_daily_normal()
    {
        var vm = New(Clock());

        Assert.True(vm.IsNew);
        Assert.True(vm.IsDaily);
        Assert.Contains(vm.Urgencies, u => u.IsChecked && u.Profile.Level == UrgencyLevel.Normal);
    }

    [Fact]
    public void Empty_title_fails_validation()
    {
        var vm = New(Clock());
        vm.Title = "   ";

        var (result, error) = Save(vm);

        Assert.Null(result);
        Assert.NotNull(error);
    }

    [Fact]
    public void Daily_builds_a_daily_schedule()
    {
        var vm = New(Clock());
        vm.Title = "Reunião";
        vm.IsDaily = true;
        vm.TimeText = "07:30";

        var (result, error) = Save(vm);

        Assert.Null(error);
        var daily = Assert.IsType<DailySchedule>(result!.Schedule);
        Assert.Equal(new TimeOnly(7, 30), daily.At);
    }

    [Fact]
    public void Invalid_time_fails_validation()
    {
        var vm = New(Clock());
        vm.Title = "Reunião";
        vm.IsDaily = true;
        vm.TimeText = "25:99";

        var (result, _) = Save(vm);

        Assert.Null(result);
    }

    [Fact]
    public void Once_in_the_past_fails_validation()
    {
        var clock = Clock();
        var vm = New(clock);
        vm.Title = "Consulta";
        vm.IsOnce = true;
        vm.DateText = "06/10/2026"; // MM/dd/yyyy (English)
        vm.TimeText = "08:00";      // antes das 09:00 de agora

        var (result, _) = Save(vm);

        Assert.Null(result);
    }

    [Fact]
    public void Once_in_the_future_builds_a_one_time_schedule()
    {
        var vm = New(Clock());
        vm.Title = "Consulta";
        vm.IsOnce = true;
        vm.DateText = "06/10/2026";
        vm.TimeText = "10:00";

        var (result, error) = Save(vm);

        Assert.Null(error);
        Assert.IsType<OneTimeSchedule>(result!.Schedule);
    }

    [Fact]
    public void Weekly_without_days_fails_validation()
    {
        var vm = New(Clock());
        vm.Title = "Standup";
        vm.IsWeekly = true;
        vm.TimeText = "09:00";
        foreach (var d in vm.Days)
        {
            d.IsChecked = false;
        }

        var (result, _) = Save(vm);

        Assert.Null(result);
    }

    [Fact]
    public void Weekly_builds_a_weekly_schedule_with_the_checked_days()
    {
        var vm = New(Clock());
        vm.Title = "Standup";
        vm.IsWeekly = true;
        vm.TimeText = "09:00";
        foreach (var d in vm.Days)
        {
            d.IsChecked = d.Flag is WeekDays.Monday or WeekDays.Friday;
        }

        var (result, error) = Save(vm);

        Assert.Null(error);
        var weekly = Assert.IsType<WeeklySchedule>(result!.Schedule);
        Assert.Equal(WeekDays.Monday | WeekDays.Friday, weekly.Days);
    }

    [Fact]
    public void Interval_builds_an_interval_schedule_with_a_window()
    {
        var vm = New(Clock());
        vm.Title = "Água";
        vm.IsInterval = true;
        vm.IntervalMinutesText = "45";
        vm.UseWindow = true;
        vm.WindowFromText = "09:00";
        vm.WindowToText = "18:00";

        var (result, error) = Save(vm);

        Assert.Null(error);
        var interval = Assert.IsType<IntervalSchedule>(result!.Schedule);
        Assert.Equal(TimeSpan.FromMinutes(45), interval.Every);
        Assert.Equal(new TimeOnly(9, 0), interval.ActiveFrom);
        Assert.Equal(new TimeOnly(18, 0), interval.ActiveTo);
    }

    [Fact]
    public void Interval_with_zero_minutes_fails_validation()
    {
        var vm = New(Clock());
        vm.Title = "Água";
        vm.IsInterval = true;
        vm.IntervalMinutesText = "0";

        var (result, _) = Save(vm);

        Assert.Null(result);
    }

    [Fact]
    public void Window_with_equal_start_and_end_fails_validation()
    {
        var vm = New(Clock());
        vm.Title = "Água";
        vm.IsInterval = true;
        vm.IntervalMinutesText = "45";
        vm.UseWindow = true;
        vm.WindowFromText = "09:00";
        vm.WindowToText = "09:00";

        var (result, _) = Save(vm);

        Assert.Null(result);
    }

    [Fact]
    public void Presence_and_escalation_toggles_are_carried_into_the_alarm()
    {
        var vm = New(Clock());
        vm.Title = "Água";
        vm.IsInterval = true;
        vm.IntervalMinutesText = "45";
        vm.UseWindow = false;
        vm.SkipWhenAway = true;
        vm.Escalate = true;

        var (result, error) = Save(vm);

        Assert.Null(error);
        Assert.NotNull(result!.SkipIfIdleFor);
        Assert.NotNull(result.Escalation);
    }

    [Fact]
    public void Editing_keeps_the_same_id()
    {
        var existente = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)));
        var vm = New(Clock(), existente);
        vm.Title = "Reunião editada";

        var (result, error) = Save(vm);

        Assert.Null(error);
        Assert.Equal(existente.Id, result!.Id);
        Assert.False(vm.IsNew);
    }
}
