using AlarmClock.App.Services;
using AlarmClock.App.ViewModels;
using AlarmClock.Core.Localization;
using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlarmClock.App.Tests;

public class MainViewModelHabitTests
{
    // Match habits by emoji, which is language independent. | Casa os hábitos pelo emoji, que independe de idioma.
    private static readonly string WaterEmoji = HabitCatalog.Find("water")!.Emoji;

    private static (MainViewModel Vm, AlarmsService Alarms) Build()
    {
        Loc.Set(AppLanguage.English);

        var clock = new FixedClock(new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero));
        var store = new InMemoryAlarmStore();
        var alarms = new AlarmsService(store, NullLogger<AlarmsService>.Instance);
        var scheduler = new AlarmScheduler(clock);
        var dialogs = new AlarmDialogs(clock);
        var startup = new StartupRegistrar(NullLogger<StartupRegistrar>.Instance);

        var vm = new MainViewModel(alarms, scheduler, dialogs, startup, clock, NullLogger<MainViewModel>.Instance);
        return (vm, alarms);
    }

    private static HabitRowViewModel Water(MainViewModel vm) =>
        vm.Habits.First(h => h.Emoji == WaterEmoji);

    [Fact]
    public void Habits_start_listed_and_inactive()
    {
        var (vm, _) = Build();

        Assert.Equal(HabitCatalog.All.Count, vm.Habits.Count);
        Assert.All(vm.Habits, h => Assert.False(h.IsActive));
    }

    [Fact]
    public void Activating_a_habit_creates_its_alarm()
    {
        var (vm, alarms) = Build();

        Water(vm).IsActive = true;

        var alarme = Assert.Single(alarms.Items);
        Assert.Equal("water", alarme.HabitKey);
        Assert.IsType<IntervalSchedule>(alarme.Schedule);
        Assert.True(Water(vm).IsActive);
    }

    [Fact]
    public void Deactivating_a_habit_removes_its_alarm()
    {
        var (vm, alarms) = Build();
        Water(vm).IsActive = true;

        Water(vm).IsActive = false;

        Assert.Empty(alarms.Items);
        Assert.False(Water(vm).IsActive);
    }

    [Fact]
    public void Changing_the_interval_of_an_active_habit_updates_the_alarm()
    {
        var (vm, alarms) = Build();
        Water(vm).IsActive = true;

        Water(vm).IntervalText = "30";

        var agenda = Assert.IsType<IntervalSchedule>(alarms.Items.Single().Schedule);
        Assert.Equal(TimeSpan.FromMinutes(30), agenda.Every);
    }

    [Fact]
    public void Habits_never_show_in_the_alarms_list()
    {
        var (vm, _) = Build();

        Water(vm).IsActive = true;

        Assert.Empty(vm.Alarms);
    }

    [Fact]
    public void User_alarms_show_in_the_alarms_list_and_not_among_habits()
    {
        var (vm, alarms) = Build();

        alarms.AddOrUpdate(Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0))));

        Assert.Single(vm.Alarms);
        Assert.Equal(HabitCatalog.All.Count, vm.Habits.Count);
    }
}
