using System.Windows;
using System.Windows.Controls;

namespace BHGKeyMan.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void SecretValuePasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.UpdateSecretValueInput(passwordBox.Password);
        }
    }
}
