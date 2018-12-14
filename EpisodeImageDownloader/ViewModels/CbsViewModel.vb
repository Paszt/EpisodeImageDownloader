Imports CsQuery
Imports System.Text.RegularExpressions
Imports System.Collections.ObjectModel

Namespace ViewModels

    Public Class CbsViewModel
        Inherits ViewModels.ViewModelBase

        ''Private _client As Infrastructure.ChromeWebClient

        Public Sub New()
            EpisodeInfos = New ObservableCollection(Of EpisodeInfo)
        End Sub

#Region " Properties "

        Private _showName As String
        Public Property ShowName As String
            Get
                Return _showName
            End Get
            Set(value As String)
                SetProperty(_showName, value)
            End Set
        End Property

        Private _seasonNumber As Integer?
        Public Property SeasonNumber As Integer?
            Get
                Return _seasonNumber
            End Get
            Set(value As Integer?)
                SetProperty(_seasonNumber, value)
            End Set
        End Property

        Private _episodeInfos As ObservableCollection(Of EpisodeInfo)
        Public Property EpisodeInfos As ObservableCollection(Of EpisodeInfo)
            Get
                Return _episodeInfos
            End Get
            Set(value As ObservableCollection(Of EpisodeInfo))
                SetProperty(_episodeInfos, value)
            End Set
        End Property

        Public ReadOnly Property EpisodeDownloadFolder(EpisodeNumber As Integer) As String
            Get
                Return IO.Path.Combine(My.Settings.DownloadFolder,
                                       ShowName.MakeFileNameSafe,
                                       "Season " & CInt(SeasonNumber).ToString("00"),
                                       "S" & CInt(SeasonNumber).ToString("00") & "E" & CInt(EpisodeNumber).ToString("00"))
            End Get
        End Property

        Public ReadOnly Property SeasonDownloadFolder As String
            Get
                If SeasonNumber.HasValue AndAlso Not String.IsNullOrWhiteSpace(ShowName) Then
                    Return IO.Path.Combine(My.Settings.DownloadFolder,
                                           ShowName.MakeFileNameSafe,
                                           "Season " & CInt(SeasonNumber).ToString("00"))
                Else
                    Return String.Empty
                End If
            End Get
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return EpisodeInfos.Count > 0 AndAlso
                   EpisodeInfoValid() AndAlso
                   Not String.IsNullOrEmpty(ShowName) AndAlso
                   SeasonNumber.HasValue
        End Function

        Private Function EpisodeInfoValid() As Boolean
            Return EpisodeInfos.Where(Function(e) Not String.IsNullOrWhiteSpace(e.EpisodeUrl)).Count() = EpisodeInfos.Count()
            'Dim returnValue As Boolean = True
            'For Each info In EpisodeInfos
            '    If info.EpisodeNumber.HasValue AndAlso Not String.IsNullOrEmpty(info.EpisodeUrl) Then
            '        Return True
            '    End If
            'Next
            'Return False
        End Function

        Protected Overrides Sub DownloadImages()
            Parallel.ForEach(EpisodeInfos, AddressOf DownloadEpisodeImages)
        End Sub

        Private Sub DownloadEpisodeImages(info As EpisodeInfo)
            If info.EpisodeNumber.HasValue Then
                Dim counter As Integer = 0
                Dim pageUri As Uri = New Uri(info.EpisodeUrl)
                Dim client = New Infrastructure.ChromeWebClient() With {.AllowAutoRedirect = True}
                Do
                    DownloadImage(pageUri, info.EpisodeNumber.Value, client)
                    counter += 1
                    If counter > 50 Then
                        If MessageWindow.ShowDialog("It appears a endless loop has been encountered.  Should I quit?", "Possible endless loop", True) = False Then
                            Exit Do
                        End If
                    End If
                Loop Until pageUri = Nothing OrElse pageUri.ToString.EndsWith("/0/")
            End If
        End Sub

        Private Sub DownloadImage(ByRef pageUri As Uri, episodeNumber As Integer, client As Infrastructure.ChromeWebClient)
            If Not IO.Directory.Exists(EpisodeDownloadFolder(episodeNumber)) Then
                IO.Directory.CreateDirectory(EpisodeDownloadFolder(episodeNumber))
            End If

            Dim html As String
            Try
                html = client.DownloadString(pageUri)
            Catch ex As Exception
                MessageWindow.ShowDialog("Error downloading page:" & ex.Message & Environment.NewLine & Environment.NewLine & pageUri.ToString, "Download error")
                pageUri = Nothing
                Exit Sub
            End Try

            Dim cqDoc As CQ
            Try
                cqDoc = CQ.CreateDocument(html)
            Catch ex As Exception
                MessageWindow.ShowDialog("Error parsing html: " & ex.Message & Environment.NewLine & Environment.NewLine & pageUri.ToString, "Error parsing data")
                pageUri = Nothing
                Exit Sub
            End Try

            Dim imageUrl = cqDoc("#large-img-wrapper-0 img").Attr("src")
            If String.IsNullOrWhiteSpace(imageUrl) Then
                AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = "Episode " & episodeNumber, .HasError = True, .NewDownload = False, .Message = "No Image found! " & pageUri.ToString})
            Else
                imageUrl = Regex.Replace(imageUrl, "\/styles\/.+\/public", String.Empty)
                Dim Filename = IO.Path.GetFileName(New Uri(imageUrl).AbsolutePath)
                Dim localPath = IO.Path.Combine(EpisodeDownloadFolder(episodeNumber), Filename)
                Dim SeasonEpisodeFilename = "S" & SeasonNumber.Value.ToString("00") & "E" & episodeNumber.ToString("00") & " - " & Filename
                If Not IO.File.Exists(localPath) Then
                    Try
                        client.DownloadFile(imageUrl, localPath)
                        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
                    Catch ex As Exception
                        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
                    End Try
                Else
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = False, .NewDownload = False, .Message = "Already Downloaded"})
                End If
            End If

            Dim nextPageUrl As String = cqDoc(".phoNextImg").Parent().Attr("href")
            If Not nextPageUrl.EndsWith("/") Then
                nextPageUrl = nextPageUrl & "/"
            End If
            If Not String.IsNullOrWhiteSpace(nextPageUrl) Then
                If Uri.TryCreate(nextPageUrl, UriKind.RelativeOrAbsolute, pageUri) Then
                    If Not pageUri.IsAbsoluteUri Then
                        pageUri = New Uri(New Uri("http://www.cbs.com/"), pageUri.ToString())
                    End If
                Else
                    pageUri = Nothing
                End If
            Else
                pageUri = Nothing
            End If
        End Sub

        Public Function SeasonDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(SeasonDownloadFolder)
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(Sub()
                                                           Process.Start(SeasonDownloadFolder)
                                                       End Sub, AddressOf SeasonDownloadFolderExists)
            End Get
        End Property

    End Class

End Namespace