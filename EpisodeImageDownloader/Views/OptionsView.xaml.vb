Namespace Views

    Public Class OptionsView

        Private Sub BrowseDownloadFolderButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowseDownloadFolderButton.Click
            Dim ofd As New System.Windows.Forms.FolderBrowserDialog()
            If ofd.ShowDialog = Forms.DialogResult.OK Then
                My.Settings.DownloadFolder = ofd.SelectedPath
            End If
        End Sub

    End Class

End Namespace
