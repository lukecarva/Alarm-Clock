using AlarmClock.Core.Localization;
using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Tests;

/// <summary>Checks that the domain produces text in both languages. | Confere que o domínio produz texto nos dois idiomas.</summary>
public class LocalizationTests : IDisposable
{
    public void Dispose()
    {
        Loc.Set(AppLanguage.Portuguese);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Ingles_traduz_a_descricao_da_agenda()
    {
        Loc.Set(AppLanguage.English);

        var diario = new DailySchedule(new TimeOnly(7, 0));
        var semanal = new WeeklySchedule(WeekDays.Weekdays, new TimeOnly(8, 30));

        Assert.Equal("Every day, 07:00", diario.Describe());
        Assert.Equal("Weekdays, 08:30", semanal.Describe());
    }

    [Fact]
    public void Portugues_traduz_a_descricao_da_agenda()
    {
        Loc.Set(AppLanguage.Portuguese);

        var diario = new DailySchedule(new TimeOnly(7, 0));

        Assert.Equal("Todo dia, 07:00", diario.Describe());
    }

    [Fact]
    public void Nome_de_urgencia_muda_com_o_idioma()
    {
        Loc.Set(AppLanguage.English);
        Assert.Equal("Critical", Loc.UrgencyName(Model.UrgencyLevel.Critical));

        Loc.Set(AppLanguage.Portuguese);
        Assert.Equal("Crítico", Loc.UrgencyName(Model.UrgencyLevel.Critical));
    }

    [Theory]
    [InlineData("pt-BR", AppLanguage.Portuguese)]
    [InlineData("pt", AppLanguage.Portuguese)]
    [InlineData("en", AppLanguage.English)]
    [InlineData("en-US", AppLanguage.English)]
    [InlineData(null, AppLanguage.English)]
    [InlineData("", AppLanguage.English)]
    public void Parse_mapeia_codigos_para_idiomas(string? code, AppLanguage esperado)
    {
        Assert.Equal(esperado, Loc.Parse(code));
    }
}
