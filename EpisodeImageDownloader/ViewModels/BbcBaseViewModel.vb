Option Strict On
Imports System.ComponentModel
Imports System.Runtime.Serialization
Imports System.Text.RegularExpressions
Imports CsQuery

Namespace ViewModels

    <DataContract>
    Public MustInherit Class BbcBaseViewModel
        Inherits ViewModelBase
        Implements INotifyDataErrorInfo

#Region " Properties "

        Private _showName As String
        <DataMember(Order:=1)>
        Public Property ShowName As String
            Get
                Return _showName
            End Get
            Set(value As String)
                SetProperty(_showName, value)
                Validate()
            End Set
        End Property

        Private _imageSize As String = "raw"
        <DataMember(Order:=2)>
        Public Property ImageSize As String
            Get
                Return _imageSize
            End Get
            Set(value As String)
                SetProperty(_imageSize, value)
                Validate()
            End Set
        End Property

        Public ReadOnly Property SeasonDownloadFolder(SeasonNumber As Integer) As String
            Get
                If Not String.IsNullOrWhiteSpace(ShowName) Then
                    Return IO.Path.Combine(ShowDownloadFolder,
                                           "Season " & SeasonNumber.ToString("00"))
                Else
                    Return String.Empty
                End If
            End Get
        End Property

        Public ReadOnly Property ShowDownloadFolder As String
            Get
                If Not String.IsNullOrWhiteSpace(ShowName) Then
                    Return IO.Path.Combine(My.Settings.DownloadFolder,
                                           ShowName.MakeFileNameSafe)
                Else
                    Return String.Empty
                End If
            End Get
        End Property

#End Region

#Region " Protected Helper Functions "

        Protected Function ImageSizeValid() As Boolean
            Return ImageSize.ToLower() = "none" OrElse ImageSize.ToLower() = "raw" OrElse Regex.IsMatch(ImageSize, "^\d+x\d+$")
        End Function

        Protected Function GetEpisodeBroadcastsUri(queryObj As CQ, baseuri As Uri) As Uri
            Dim episodeUrl = queryObj.Find("h2 a").Attr("href") & "/broadcasts.inc"
            Dim episodeUri = New Uri(episodeUrl, UriKind.RelativeOrAbsolute)
            If Not episodeUri.IsAbsoluteUri Then
                episodeUri = New Uri(baseuri, episodeUri)
            End If
            Return episodeUri
        End Function

        Protected Function GetEpisodeFirstAired(episodeUri As Uri) As String
            Dim html As String
            Try
                html = Infrastructure.WebResources.DownloadString(episodeUri)
            Catch ex As Exception
                Return Nothing
            End Try
            Dim cqDoc = CQ.Create(html)
            'Return cqDoc.Find("li span.broadcast-event__date").First().Text().ToIso8601DateString()
            Return cqDoc.Find("li div.broadcast-event__time").First().Attr("content").ToIso8601DateString()
        End Function

        Protected Function GetFullSizeImageUrl(originalUrl As String) As String
            Return Regex.Replace(originalUrl, "\/\d+x\d+\/", "/" & ImageSize & "/")
        End Function

#End Region

        Public ReadOnly Property ImageSizes As List(Of String)
            Get
                Return New List(Of String)(New String() {"1920x1080", "1280x720", "960x540", "raw", "None"})
            End Get
        End Property

        Public Function ShowDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(ShowDownloadFolder)
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           Process.Start(ShowDownloadFolder)
                                                       End Sub, AddressOf ShowDownloadFolderExists)
            End Get
        End Property


#Region " INotifyDataErrorInfo Members "

        'Public Event ErrorsChanged As EventHandler(Of DataErrorsChangedEventArgs) Implements INotifyDataErrorInfo.ErrorsChanged
        'Protected _errors As New Dictionary(Of String, List(Of String))()

        'Public ReadOnly Property HasErrors As Boolean Implements INotifyDataErrorInfo.HasErrors
        '    Get
        '        Try
        '            Return (_errors.Where(Function(c) c.Value.Count > 0).Count() > 0)
        '        Catch ex As Exception
        '            'swallow
        '        End Try
        '        Return Nothing
        '    End Get
        'End Property

        'Public Function GetErrors(propertyName As String) As IEnumerable Implements INotifyDataErrorInfo.GetErrors
        '    If String.IsNullOrEmpty(propertyName) Then
        '        Return (_errors.Values)
        '    End If
        '    EnsurePropertyErrorList(propertyName)
        '    Return _errors(propertyName)
        'End Function

        'Private Sub NotifyErrorsChanged(propertyName As String)
        '    RaiseEvent ErrorsChanged(Me, New DataErrorsChangedEventArgs(propertyName))
        'End Sub

        'Public Sub ClearError(propertyName As String)
        '    EnsurePropertyErrorList(propertyName)
        '    _errors(propertyName).Clear()
        '    NotifyErrorsChanged(propertyName)
        'End Sub

        'Public Sub AddError(propertyName As String, [error] As String)
        '    EnsurePropertyErrorList(propertyName)
        '    _errors(propertyName).Add([error])
        '    NotifyErrorsChanged(propertyName)
        'End Sub

        'Private Sub EnsurePropertyErrorList(propertyName As String)
        '    If _errors Is Nothing Then
        '        _errors = New Dictionary(Of String, List(Of String))()
        '    End If
        '    If Not _errors.ContainsKey(propertyName) Then
        '        _errors(propertyName) = New List(Of String)()
        '    End If
        'End Sub

        ''' <summary>
        ''' Force the object to validate itself using the assigned business rules.
        ''' </summary>
        ''' <param name="propertyName">Name of the property to validate.</param>
        Protected Overrides Sub Validate(<Runtime.CompilerServices.CallerMemberName> Optional propertyName As String = Nothing)
            If String.IsNullOrEmpty(propertyName) Then
                Return
            End If

            Select Case propertyName
                Case = "ShowName"
                    If String.IsNullOrWhiteSpace(ShowName) Then
                        AddError(propertyName, "Showname cannot be blank")
                    Else
                        ClearError(propertyName)
                    End If
                Case "ImageSize"
                    If ImageSizeValid() Then
                        ClearError(propertyName)
                    Else
                        AddError(propertyName, "Invalid Image size")
                    End If
            End Select
        End Sub

#End Region

    End Class

End Namespace
