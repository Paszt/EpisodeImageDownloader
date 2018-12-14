Class MainWindow

    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        SetPlacement(My.Settings.MainWindowPlacement)

        If String.IsNullOrWhiteSpace(My.Settings.DownloadFolder) OrElse Not IO.Directory.Exists(My.Settings.DownloadFolder) Then
            OptionsTabItem.BringIntoView()
            MainTabControl.SelectedItem = OptionsTabItem
        End If

        'Dim hbonew As New ViewModels.HboNewViewModel
        'hbonew.Test()
    End Sub

    Private Sub MainWindow_Closing(sender As Object, e As ComponentModel.CancelEventArgs) Handles Me.Closing
        My.Settings.MainWindowPlacement = GetPlacement()
        My.Settings.Save()
    End Sub

    Private Sub RandomDownloadButton_Click(sender As Object, e As RoutedEventArgs) Handles RandomDownloadButton.Click
        Dim rnd As New RandomViewModel()
        rnd.Begin()
    End Sub

End Class
