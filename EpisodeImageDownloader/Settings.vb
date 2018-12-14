'This class allows you to handle specific events on the settings class:
' The SettingChanging event is raised before a setting's value is changed.
' The PropertyChanged event is raised after a setting's value is changed.
' The SettingsLoaded event is raised after the setting values are loaded.
' The SettingsSaving event is raised before the setting values are saved.
Partial Friend NotInheritable Class MySettings

    Private Sub MySettings_PropertyChanged(sender As Object, e As ComponentModel.PropertyChangedEventArgs) Handles Me.PropertyChanged
        If e.PropertyName = "DownloadFolder" Then
            My.Application.InitializeFolderWatcher()
        End If
    End Sub

End Class
