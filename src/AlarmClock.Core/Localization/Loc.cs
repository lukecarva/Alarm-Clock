using System.Globalization;
using AlarmClock.Core.Model;

namespace AlarmClock.Core.Localization;

public enum AppLanguage
{
    English,
    Portuguese,
}

/// <summary>
/// Localização do app, em dois idiomas. É baseada em código (dois dicionários)
/// em vez de .resx: para um app deste tamanho, evita satélites e problemas de
/// geração de acessador fora do Visual Studio, e o conteúdo fica direto de
/// revisar. O idioma é escolhido na instalação e lido no arranque; não há troca
/// pela interface.
/// </summary>
public static class Loc
{
    /// <summary>Idioma neutro/fallback: inglês.</summary>
    public static AppLanguage Language { get; private set; } = AppLanguage.English;

    /// <summary>Cultura para formatar datas e números no idioma atual.</summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    public static void Set(AppLanguage language)
    {
        Language = language;
        Culture = CultureInfo.GetCultureInfo(language == AppLanguage.Portuguese ? "pt-BR" : "en-US");
    }

    /// <summary>Converte o código salvo/escolhido ("pt-BR", "en"…) em idioma.</summary>
    public static AppLanguage Parse(string? code) =>
        code?.StartsWith("pt", StringComparison.OrdinalIgnoreCase) == true
            ? AppLanguage.Portuguese
            : AppLanguage.English;

    public static string Code => Language == AppLanguage.Portuguese ? "pt-BR" : "en";

    public static string Get(string key)
    {
        var table = Language == AppLanguage.Portuguese ? Pt : En;
        if (table.TryGetValue(key, out var value))
        {
            return value;
        }

        // Fallback para inglês, e por fim a própria chave (denuncia tradução ausente).
        return En.TryGetValue(key, out var fallback) ? fallback : key;
    }

    public static string Format(string key, params object[] args) =>
        string.Format(Culture, Get(key), args);

    public static string UrgencyName(UrgencyLevel level) => Get($"Urgency_{level}");

    private static readonly Dictionary<string, string> En = new(StringComparer.Ordinal)
    {
        ["App_Name"] = "Productivity Alarm",

        ["Tray_Open"] = "Open",
        ["Tray_Exit"] = "Exit",

        ["Status_NextPrefix"] = "Next alarm {0}",
        ["Status_NoneConfigured"] = "No alarms set",
        ["Status_NoneActive"] = "No active alarms",
        ["Tray_Idle"] = "{0}, no active alarm",

        ["Rel_InMin"] = "in {0} min",
        ["Rel_Today"] = "today, {0}",
        ["Rel_Tomorrow"] = "tomorrow, {0}",
        ["Fmt_DateShort"] = "MM/dd",
        ["Fmt_DateLong"] = "MM/dd/yyyy",

        ["Dur_LessThanMinute"] = "less than a minute",
        ["Dur_Min"] = "{0} min",
        ["Dur_HourMin"] = "{0}h{1:00}",
        ["Dur_Days"] = "{0} day(s)",

        ["Urgency_Whisper"] = "Whisper",
        ["Urgency_Normal"] = "Normal",
        ["Urgency_High"] = "Important",
        ["Urgency_Critical"] = "Critical",

        ["Sched_At"] = "at",
        ["Sched_EveryDay"] = "Every day, {0}",
        ["Sched_Weekdays"] = "Weekdays, {0}",
        ["Sched_Weekend"] = "Weekend, {0}",
        ["Sched_Every"] = "Every {0}",
        ["Sched_WindowSep"] = "to",
        ["Day_0"] = "Sun",
        ["Day_1"] = "Mon",
        ["Day_2"] = "Tue",
        ["Day_3"] = "Wed",
        ["Day_4"] = "Thu",
        ["Day_5"] = "Fri",
        ["Day_6"] = "Sat",

        ["Alert_WindowTitle"] = "Alarm",
        ["Alert_Dismiss"] = "Got it",
        ["Alert_HoldToDismiss"] = "Hold 3s to dismiss",
        ["Alert_UseMainScreen"] = "Use the main screen to dismiss",
        ["Alert_TypePre"] = "Type",
        ["Alert_TypePost"] = "to dismiss",
        ["Alert_SnoozeBy"] = "Snooze for:",
        ["Alert_When_Snoozed"] = "You snoozed until {0}",
        ["Alert_When_Scheduled"] = "Set for {0}",
        ["Alert_When_Missed"] = "Missed: was {0}, {1} ago",
        ["Alert_When_MissedExtra"] = "{0} (+{1} earlier occurrence(s) also missed)",
        ["Alert_When_Escalated"] = "Ignored, escalated to {0}",

        ["Snooze_NotFound"] = "This alarm no longer exists.",
        ["Snooze_NotAllowed"] = "The {0} level doesn't allow snoozing.",
        ["Snooze_Once"] = "You already snoozed this alarm once.",
        ["Snooze_Max"] = "Snooze limit of {0} reached.",

        ["Row_Off"] = "off",
        ["Row_NoNext"] = "no upcoming run",
        ["Row_On"] = "On",
        ["Row_Edit"] = "Edit",
        ["Row_Delete"] = "Delete",

        ["Main_NewAlarm"] = "New alarm",
        ["Main_EmptyTitle"] = "No alarms yet",
        ["Main_EmptyBody"] = "Create your first and pick an urgency level. Whisper is a silent toast; Critical covers the whole screen and only leaves with a deliberate action.",
        ["Main_EmptyTray"] = "Closing this window doesn't quit the app. It stays in the tray.",
        ["Main_StartWithWindows"] = "Start with Windows",
        ["Main_OpenDataFolder"] = "Open data folder",
        ["Main_DataFolder"] = "Data and logs: {0}",
        ["Main_Version"] = "version {0}",

        ["Editor_New"] = "New alarm",
        ["Editor_Edit"] = "Edit alarm",
        ["Editor_Name"] = "NAME",
        ["Editor_Detail"] = "DETAIL (OPTIONAL)",
        ["Editor_When"] = "WHEN",
        ["Editor_Once"] = "Once",
        ["Editor_Daily"] = "Every day",
        ["Editor_Weekly"] = "Days of week",
        ["Editor_Interval"] = "Every…",
        ["Editor_EveryPre"] = "Every",
        ["Editor_Minutes"] = "minutes",
        ["Editor_WindowToggle"] = "Only within a time range",
        ["Editor_From"] = "from",
        ["Editor_To"] = "to",
        ["Editor_MidnightNote"] = "The range can cross midnight, like 22:00 to 06:00.",
        ["Editor_Date"] = "DATE",
        ["Editor_Time"] = "TIME",
        ["Editor_Presence"] = "PRESENCE",
        ["Editor_SkipAway"] = "Don't alert if I'm away",
        ["Editor_SkipAwayNote"] = "Skips the alert if keyboard and mouse have been idle for over 5 minutes. Applies to on-time firing; missed alarms and snoozes follow the level's rules.",
        ["Editor_Insistence"] = "INSISTENCE",
        ["Editor_Escalate"] = "Escalate if I ignore it",
        ["Editor_EscalateNote"] = "Ignored for 10 minutes or snoozed twice, the alarm returns one level higher, up to Critical. Dismissing for good ends the escalation.",
        ["Editor_Urgency"] = "URGENCY",
        ["Editor_Sound"] = "SOUND (OPTIONAL)",
        ["Editor_SoundChoose"] = "Choose…",
        ["Editor_SoundClear"] = "Clear",
        ["Editor_SoundNote"] = "Empty uses the app's built-in tone.",
        ["Editor_Cancel"] = "Cancel",
        ["Editor_Save"] = "Save",
        ["Editor_SoundDialogTitle"] = "Alarm sound",
        ["Editor_SoundFilter"] = "Audio (*.wav;*.mp3;*.m4a;*.wma)|*.wav;*.mp3;*.m4a;*.wma|All files|*.*",

        ["Val_NeedName"] = "Give the alarm a name.",
        ["Val_BadTime"] = "Invalid time. Use HH:mm, e.g. 07:30.",
        ["Val_BadInterval"] = "Invalid interval. Enter the minutes, e.g. 45.",
        ["Val_BadWindow"] = "Invalid time range. Use HH:mm in both fields.",
        ["Val_WindowEqual"] = "The time range needs a different start and end.",
        ["Val_BadDate"] = "Invalid date. Use {0}.",
        ["Val_PastInstant"] = "That moment has already passed.",
        ["Val_SaveFailed"] = "Couldn't save the alarm.",

        ["Confirm_Delete"] = "Delete the alarm \"{0}\"?",
        ["Error_UIBody"] = "An unexpected UI error occurred. The alarm keeps running.\n\n{0}\n\nDetails at: {1}",
    };

    private static readonly Dictionary<string, string> Pt = new(StringComparer.Ordinal)
    {
        ["App_Name"] = "Despertador Produtivo",

        ["Tray_Open"] = "Abrir",
        ["Tray_Exit"] = "Sair",

        ["Status_NextPrefix"] = "Próximo alarme {0}",
        ["Status_NoneConfigured"] = "Nenhum alarme configurado",
        ["Status_NoneActive"] = "Nenhum alarme ativo",
        ["Tray_Idle"] = "{0}, nenhum alarme ativo",

        ["Rel_InMin"] = "em {0} min",
        ["Rel_Today"] = "hoje, {0}",
        ["Rel_Tomorrow"] = "amanhã, {0}",
        ["Fmt_DateShort"] = "dd/MM",
        ["Fmt_DateLong"] = "dd/MM/yyyy",

        ["Dur_LessThanMinute"] = "menos de um minuto",
        ["Dur_Min"] = "{0} min",
        ["Dur_HourMin"] = "{0}h{1:00}",
        ["Dur_Days"] = "{0} dia(s)",

        ["Urgency_Whisper"] = "Sussurro",
        ["Urgency_Normal"] = "Normal",
        ["Urgency_High"] = "Importante",
        ["Urgency_Critical"] = "Crítico",

        ["Sched_At"] = "às",
        ["Sched_EveryDay"] = "Todo dia, {0}",
        ["Sched_Weekdays"] = "Dias úteis, {0}",
        ["Sched_Weekend"] = "Fim de semana, {0}",
        ["Sched_Every"] = "A cada {0}",
        ["Sched_WindowSep"] = "às",
        ["Day_0"] = "dom",
        ["Day_1"] = "seg",
        ["Day_2"] = "ter",
        ["Day_3"] = "qua",
        ["Day_4"] = "qui",
        ["Day_5"] = "sex",
        ["Day_6"] = "sáb",

        ["Alert_WindowTitle"] = "Alarme",
        ["Alert_Dismiss"] = "Ok, entendi",
        ["Alert_HoldToDismiss"] = "Segure 3s para dispensar",
        ["Alert_UseMainScreen"] = "Use a tela principal para dispensar",
        ["Alert_TypePre"] = "Digite",
        ["Alert_TypePost"] = "para dispensar",
        ["Alert_SnoozeBy"] = "Adiar por:",
        ["Alert_When_Snoozed"] = "Você adiou até {0}",
        ["Alert_When_Scheduled"] = "Marcado para {0}",
        ["Alert_When_Missed"] = "Perdido: era {0}, {1} atrás",
        ["Alert_When_MissedExtra"] = "{0} (+{1} ocorrência(s) anteriores também perdidas)",
        ["Alert_When_Escalated"] = "Ignorado, subiu para {0}",

        ["Snooze_NotFound"] = "Este alarme não existe mais.",
        ["Snooze_NotAllowed"] = "O nível {0} não permite adiar.",
        ["Snooze_Once"] = "Você já adiou este alarme uma vez.",
        ["Snooze_Max"] = "Limite de {0} adiamentos atingido.",

        ["Row_Off"] = "desligado",
        ["Row_NoNext"] = "sem próxima ocorrência",
        ["Row_On"] = "Ativo",
        ["Row_Edit"] = "Editar",
        ["Row_Delete"] = "Excluir",

        ["Main_NewAlarm"] = "Novo alarme",
        ["Main_EmptyTitle"] = "Nenhum alarme ainda",
        ["Main_EmptyBody"] = "Crie o primeiro e escolha o nível de urgência. Sussurro é um toast silencioso; Crítico cobre a tela inteira e só sai com uma ação deliberada.",
        ["Main_EmptyTray"] = "Fechar esta janela não encerra o app. Ele continua na bandeja.",
        ["Main_StartWithWindows"] = "Iniciar com o Windows",
        ["Main_OpenDataFolder"] = "Abrir pasta de dados",
        ["Main_DataFolder"] = "Dados e logs: {0}",
        ["Main_Version"] = "versão {0}",

        ["Editor_New"] = "Novo alarme",
        ["Editor_Edit"] = "Editar alarme",
        ["Editor_Name"] = "NOME",
        ["Editor_Detail"] = "DETALHE (OPCIONAL)",
        ["Editor_When"] = "QUANDO",
        ["Editor_Once"] = "Uma vez",
        ["Editor_Daily"] = "Todo dia",
        ["Editor_Weekly"] = "Dias da semana",
        ["Editor_Interval"] = "A cada…",
        ["Editor_EveryPre"] = "A cada",
        ["Editor_Minutes"] = "minutos",
        ["Editor_WindowToggle"] = "Só dentro de uma faixa de horário",
        ["Editor_From"] = "das",
        ["Editor_To"] = "às",
        ["Editor_MidnightNote"] = "A faixa pode atravessar a meia-noite, como 22:00 às 06:00.",
        ["Editor_Date"] = "DATA",
        ["Editor_Time"] = "HORA",
        ["Editor_Presence"] = "PRESENÇA",
        ["Editor_SkipAway"] = "Não alertar se eu estiver ausente",
        ["Editor_SkipAwayNote"] = "Pula o alerta se o teclado e o mouse estiverem parados há mais de 5 minutos. Vale para o disparo na hora. Alarme perdido e adiamento seguem as regras do nível.",
        ["Editor_Insistence"] = "INSISTÊNCIA",
        ["Editor_Escalate"] = "Subir de nível se eu ignorar",
        ["Editor_EscalateNote"] = "Ignorado por 10 minutos ou adiado 2 vezes, o alarme volta um nível acima, até chegar a Crítico. Dispensar de vez encerra a escalada.",
        ["Editor_Urgency"] = "URGÊNCIA",
        ["Editor_Sound"] = "SOM (OPCIONAL)",
        ["Editor_SoundChoose"] = "Escolher…",
        ["Editor_SoundClear"] = "Limpar",
        ["Editor_SoundNote"] = "Vazio usa o toque interno do app.",
        ["Editor_Cancel"] = "Cancelar",
        ["Editor_Save"] = "Salvar",
        ["Editor_SoundDialogTitle"] = "Som do alarme",
        ["Editor_SoundFilter"] = "Áudio (*.wav;*.mp3;*.m4a;*.wma)|*.wav;*.mp3;*.m4a;*.wma|Todos os arquivos|*.*",

        ["Val_NeedName"] = "Dê um nome ao alarme.",
        ["Val_BadTime"] = "Horário inválido. Use HH:mm, por exemplo 07:30.",
        ["Val_BadInterval"] = "Intervalo inválido. Informe os minutos, por exemplo 45.",
        ["Val_BadWindow"] = "Faixa de horário inválida. Use HH:mm nos dois campos.",
        ["Val_WindowEqual"] = "A faixa de horário precisa ter início e fim diferentes.",
        ["Val_BadDate"] = "Data inválida. Use {0}.",
        ["Val_PastInstant"] = "Esse instante já passou.",
        ["Val_SaveFailed"] = "Não foi possível salvar o alarme.",

        ["Confirm_Delete"] = "Excluir o alarme \"{0}\"?",
        ["Error_UIBody"] = "Ocorreu um erro inesperado na interface. O despertador continua rodando.\n\n{0}\n\nDetalhes em: {1}",
    };
}
