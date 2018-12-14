Imports System.ComponentModel
Imports System.Collections.ObjectModel
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    <Runtime.Serialization.DataContract>
    Public MustInherit Class ViewModelBase
        Implements INotifyPropertyChanged
        Implements INotifyDataErrorInfo

        Public Sub New()
            EpisodeImageResults = New ObservableCollection(Of EpisodeImageResult)
        End Sub

#Region " Properties "

        Private _episodeImageResults As ObservableCollection(Of EpisodeImageResult)
        Public Property EpisodeImageResults As ObservableCollection(Of EpisodeImageResult)
            Get
                Return _episodeImageResults
            End Get
            Set(value As ObservableCollection(Of EpisodeImageResult))
                SetProperty(_episodeImageResults, value)
            End Set
        End Property

        Private _notBusy As Boolean = True
        Public Property NotBusy As Boolean
            Get
                Return _notBusy
            End Get
            Set(value As Boolean)
                If SetProperty(_notBusy, value) Then
                    If value Then
                        My.Application.BorderColor = CType(Application.Current.Resources("BackgroundSelected"), SolidColorBrush)
                    Else
                        My.Application.BorderColor = CType(Application.Current.Resources("BusyBorderBrush"), SolidColorBrush)
                    End If
                End If
            End Set
        End Property

#End Region

        ''' <summary>Download an image and add the result message to the EpisodeImageResults list</summary>
        ''' <param name="Url">The URL from which to download the image.</param>
        ''' <param name="localPath">The name of the local file that is to receive the data.</param>
        Protected Overloads Sub DownloadImageAddResult(Url As String, localPath As String)
            Dim Filename = IO.Path.GetFileName(localPath)
            If Not IO.File.Exists(localPath) Then
                If Not IO.Directory.Exists(IO.Path.GetDirectoryName(localPath)) Then
                    IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(localPath))
                End If
                Try
                    WebResources.DownloadFile(Url, localPath)
                    'Using _client As New Infrastructure.ChromeWebClient With {.AllowAutoRedirect = True}
                    '    _client.DownloadFile(Url, localPath)
                    'End Using
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
                Catch ex As Exception
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
                End Try
            Else
                AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
            End If
        End Sub

        ''' <summary>Download an image and add the result message to the EpisodeImageResults list</summary>
        ''' <param name="Url">The URL from which to download the image.</param>
        ''' <param name="localPath">The name of the local file that is to receive the data.</param>
        ''' <param name="FileNameToAdd">The filename to add to the result message which is added to the EpisodeImageResults list.</param>
        Protected Overloads Sub DownloadImageAddResult(Url As String, localPath As String, FileNameToAdd As String)
            'Dim Filename = IO.Path.GetFileName(localPath)
            If Not IO.File.Exists(localPath) Then
                If Not IO.Directory.Exists(IO.Path.GetDirectoryName(localPath)) Then
                    IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(localPath))
                End If
                Try
                    Using _client As New Infrastructure.ChromeWebClient With {.AllowAutoRedirect = True}
                        _client.DownloadFile(Url, localPath)
                    End Using
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = FileNameToAdd, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
                Catch ex As Exception
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = FileNameToAdd, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
                End Try
            Else
                AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = FileNameToAdd, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
            End If
        End Sub

        Public Delegate Sub AddEpisodeDelegate(result As EpisodeImageResult)

        <DebuggerStepThrough>
        Public Sub AddEpisodeImageResult(result As EpisodeImageResult)
            If Application.Current IsNot Nothing Then
                Application.Current.Dispatcher.BeginInvoke(New AddEpisodeDelegate(AddressOf EpisodeImageResults.Add), result)
            End If
        End Sub

        Public Sub ClearEpisodeImageResults()
            If Application.Current IsNot Nothing Then
                Application.Current.Dispatcher.BeginInvoke(New System.Threading.ThreadStart(AddressOf EpisodeImageResults.Clear))
            End If
        End Sub

        Public ReadOnly Property DownloadImagesCommand As ICommand
            Get
                Return New RelayCommand(Sub()
                                            Dim th As New System.Threading.Thread(AddressOf DownloadImagesInit)
                                            th.Start()
                                        End Sub, AddressOf CanDownloadImages)
            End Get
        End Property

        Private Sub DownloadImagesInit()
            NotBusy = False
            ClearEpisodeImageResults()

            DownloadImages()

            NotBusy = True
        End Sub

        Protected MustOverride Function CanDownloadImages() As Boolean
        Protected MustOverride Sub DownloadImages()

#Region " INotifyPropertyChanged Members "

        Public Event PropertyChanged(sender As Object, e As PropertyChangedEventArgs) Implements INotifyPropertyChanged.PropertyChanged

        Protected Function SetProperty(Of T)(ByRef storage As T, value As T,
                                             <Runtime.CompilerServices.CallerMemberName> Optional propertyName As String = Nothing) As Boolean
            If Equals(storage, value) Then
                Return False
            End If
            storage = value
            Me.OnPropertyChanged(propertyName)
            Return True
        End Function

        Protected Sub OnPropertyChanged(<Runtime.CompilerServices.CallerMemberName> Optional propertyName As String = Nothing)
            Dim propertyChanged As System.ComponentModel.PropertyChangedEventHandler = Me.PropertyChangedEvent
            If propertyChanged IsNot Nothing Then
                propertyChanged(Me, New System.ComponentModel.PropertyChangedEventArgs(propertyName))
            End If
        End Sub

#End Region


#Region " INotifyDataErrorInfo Members "

        Public Event ErrorsChanged As EventHandler(Of DataErrorsChangedEventArgs) Implements INotifyDataErrorInfo.ErrorsChanged
        Protected _errors As New Dictionary(Of String, List(Of String))()

        Public ReadOnly Property HasErrors As Boolean Implements INotifyDataErrorInfo.HasErrors
            Get
                Try
                    Return (_errors.Where(Function(c) c.Value.Count > 0).Count() > 0)
                Catch ex As Exception
                    'swallow
                End Try
                Return Nothing
            End Get
        End Property

        Public Function GetErrors(propertyName As String) As IEnumerable Implements INotifyDataErrorInfo.GetErrors
            If String.IsNullOrEmpty(propertyName) Then
                Return (_errors.Values)
            End If
            EnsurePropertyErrorList(propertyName)
            Return _errors(propertyName)
        End Function

        Private Sub NotifyErrorsChanged(propertyName As String)
            RaiseEvent ErrorsChanged(Me, New DataErrorsChangedEventArgs(propertyName))
        End Sub

        Public Sub ClearError(propertyName As String)
            EnsurePropertyErrorList(propertyName)
            _errors(propertyName).Clear()
            NotifyErrorsChanged(propertyName)
        End Sub

        Public Sub AddError(propertyName As String, [error] As String)
            EnsurePropertyErrorList(propertyName)
            _errors(propertyName).Add([error])
            NotifyErrorsChanged(propertyName)
        End Sub

        Private Sub EnsurePropertyErrorList(propertyName As String)
            If _errors Is Nothing Then
                _errors = New Dictionary(Of String, List(Of String))()
            End If
            If Not _errors.ContainsKey(propertyName) Then
                _errors(propertyName) = New List(Of String)()
            End If
        End Sub

        ''' <summary>
        ''' Force the object to validate itself using the assigned business rules.
        ''' </summary>
        ''' <param name="propertyName">Name of the property to validate.</param>
        Protected Overridable Sub Validate(<Runtime.CompilerServices.CallerMemberName> Optional propertyName As String = Nothing)
        End Sub

#End Region


    End Class

End Namespace
