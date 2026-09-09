using System.Windows;
using AlarmClock.App.ViewModels;
using AlarmClock.App.Views;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Model;

namespace AlarmClock.App.Services;

/// <summary>
/// Abre as janelas modais do app. Existe para as ViewModels não precisarem
/// conhecer tipos de janela — o que também as mantém testáveis.
/// </summary>
public sealed class AlarmDialogs
{
    private readonly ISystemClock _clock;

    public AlarmDialogs(ISystemClock clock) => _clock = clock;

    /// <summary>Devolve o alarme editado, ou nulo se o usuário cancelou.</summary>
    public Alarm? Edit(Alarm? existente)
    {
        var viewModel = new AlarmEditorViewModel(_clock, existente);

        var janela = new AlarmEditorWindow(viewModel)
        {
            Owner = Application.Current.MainWindow,
        };

        return janela.ShowDialog() == true ? viewModel.Result : null;
    }

    public bool ConfirmDelete(string titulo)
    {
        var resposta = MessageBox.Show(
            $"Excluir o alarme \"{titulo}\"?",
            "Despertador Produtivo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return resposta == MessageBoxResult.Yes;
    }
}
