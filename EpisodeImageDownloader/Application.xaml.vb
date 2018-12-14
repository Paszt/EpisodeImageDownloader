Imports System.ComponentModel

Class Application
    Implements INotifyPropertyChanged

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.

    Private WithEvents FolderWatcher As IO.FileSystemWatcher

#Region " Properties "

    Private _showFolders As List(Of String)
    Public Property ShowFolders As List(Of String)
        Get
            Return _showFolders
        End Get
        Set(value As List(Of String))
            SetProperty(_showFolders, value)
        End Set
    End Property

    Private _statusMessage As String
    Public Property StatusMessage As String
        Get
            Return _statusMessage
        End Get
        Set(value As String)
            If SetProperty(_statusMessage, value) Then
                OnPropertyChanged("ShowStatusBar")
            End If
        End Set
    End Property

    Public ReadOnly Property ShowStatusBar As Boolean
        Get
            Return Not String.IsNullOrEmpty(StatusMessage)
        End Get
    End Property

    Private _borderColor As SolidColorBrush = CType(Application.Current.Resources("BackgroundSelected"), SolidColorBrush)
    Public Property BorderColor As SolidColorBrush
        Get
            Return _borderColor
        End Get
        Set(value As SolidColorBrush)
            SetProperty(_borderColor, value)
        End Set
    End Property

#End Region

    Private Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        If String.IsNullOrWhiteSpace(My.Settings.DownloadFolder) Then
            My.Settings.DownloadFolder = IO.Path.Combine(My.Computer.FileSystem.SpecialDirectories.MyDocuments, "EpisodeImageDownloader")
        End If
        InitializeFolderWatcher()
    End Sub

#Region " Show Folder Watcher Methods "

    Public Sub InitializeFolderWatcher()
        If FolderWatcher IsNot Nothing Then
            FolderWatcher.EnableRaisingEvents = False
            FolderWatcher.Dispose()
        End If
        If IO.Directory.Exists(My.Settings.DownloadFolder) Then
            FolderWatcher = New IO.FileSystemWatcher(My.Settings.DownloadFolder) With {
                .IncludeSubdirectories = False,
                .NotifyFilter = IO.NotifyFilters.DirectoryName,
                .EnableRaisingEvents = True
            }
            UpdateShowFolders()
        End If
    End Sub

    Private Sub FolderWatcher_CreatedOrRenamed(sender As Object, e As IO.FileSystemEventArgs) Handles FolderWatcher.Created, FolderWatcher.Renamed
        UpdateShowFolders()
    End Sub

    Private Sub UpdateShowFolders()
        ShowFolders = (From f In IO.Directory.GetDirectories(My.Settings.DownloadFolder)
                       Select New IO.FileInfo(f).Name).ToList()
    End Sub

#End Region

#Region " INotifyPropertyChanged Members "

    Public Event PropertyChanged(sender As Object, e As PropertyChangedEventArgs) Implements INotifyPropertyChanged.PropertyChanged

    Protected Function SetProperty(Of T)(ByRef storage As T, value As T,
                                         <Runtime.CompilerServices.CallerMemberName> Optional propertyName As String = Nothing) As Boolean
        If Equals(storage, value) Then
            Return False
        End If
        storage = value
        OnPropertyChanged(propertyName)
        Return True
    End Function

    Protected Sub OnPropertyChanged(<Runtime.CompilerServices.CallerMemberName> Optional propertyName As String = Nothing)
        Dim propertyChanged As PropertyChangedEventHandler = Me.PropertyChangedEvent
        If propertyChanged IsNot Nothing Then
            propertyChanged(Me, New PropertyChangedEventArgs(propertyName))
        End If
    End Sub

#End Region

End Class
