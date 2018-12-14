Option Strict On

Namespace ViewModels

    Public Class TheColbertReportViewModel
        Inherits ViewModels.ViewModelBase

#Region " Properties "

        Private _seasonNumber As Integer?
        Public Property SeasonNumber As Integer?
            Get
                Return _seasonNumber
            End Get
            Set(value As Integer?)
                SetProperty(_seasonNumber, value)
            End Set
        End Property

        Private _episodeFrom As Integer?
        Public Property EpisodeFrom As Integer?
            Get
                Return _episodeFrom
            End Get
            Set(value As Integer?)
                SetProperty(_episodeFrom, value)
            End Set
        End Property

        Private _episodeTo As Integer?
        Public Property EpisodeTo As Integer?
            Get
                Return _episodeTo
            End Get
            Set(value As Integer?)
                SetProperty(_episodeTo, value)
            End Set
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return SeasonNumber.HasValue AndAlso EpisodeFrom.HasValue AndAlso EpisodeTo.HasValue
        End Function

        Protected Overrides Sub DownloadImages()
            ''Dim downloadFolder As String = IO.Path.Combine(My.Settings.DownloadFolder, "The Colbert Report", "Season " & SeasonNumber.Value.ToString("00"))
            If Not IO.Directory.Exists(DownloadFolder) Then
                IO.Directory.CreateDirectory(DownloadFolder)
            End If

            Using wc As New Infrastructure.ChromeWebClient()
                For episodeNumber As Integer = CInt(EpisodeFrom) To CInt(EpisodeTo) ' <--- episode numbers
                    DownloadImage(episodeNumber, String.Empty, wc)
                    DownloadImage(episodeNumber, "_sixty", wc)
                    For imageNumber = 1 To 8
                        DownloadImage(episodeNumber, "_" & imageNumber.ToString("00"), wc)
                    Next
                Next
            End Using
        End Sub

        Private Sub DownloadImage(episodeNumber As Integer, imageSuffix As String, wc As Infrastructure.ChromeWebClient)
            Dim localNotFoundFilename As String = GetLocalNotFoundFileName(CInt(SeasonNumber), CInt(episodeNumber))
            Dim url = GetImageUrl(CInt(SeasonNumber), CInt(episodeNumber), imageSuffix)
            Dim Filename = IO.Path.GetFileName(New Uri(url).AbsolutePath)
            Dim localPath = IO.Path.Combine(DownloadFolder, Filename)
            Dim localDonePath = IO.Path.Combine(DownloadFolder, "done", Filename)
            If Not IO.File.Exists(localPath) AndAlso Not IO.File.Exists(localDonePath) Then
                Try
                    wc.DownloadFile(url, localPath)
                    If wc.HttpStatusCode = Net.HttpStatusCode.Found Then
                        If IO.File.Exists(IO.Path.Combine(DownloadFolder, localNotFoundFilename)) Then
                            My.Computer.FileSystem.DeleteFile(localPath)
                        Else
                            My.Computer.FileSystem.RenameFile(localPath, localNotFoundFilename)
                        End If
                        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = True, .NewDownload = False, .Message = "Not found"})
                    Else
                        AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = False, .NewDownload = True, .Message = "Downloaded"})
                    End If
                Catch ex As Exception
                    AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = True, .NewDownload = False, .Message = "Error: " & ex.Message})
                End Try
            Else
                Dim message As String = "Already Downloaded"
                If IO.File.Exists(localDonePath) Then
                    message &= " (in done folder)"
                End If
                AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = Filename, .HasError = False, .NewDownload = False, .Message = message})
            End If
        End Sub

        Private Function DownloadFolder() As String
            Return IO.Path.Combine(My.Settings.DownloadFolder, "The Colbert Report", "Season " & SeasonNumber.Value.ToString("00"))
        End Function

        Private Function GetImageUrl(seasonNumber As Integer, episodeNumber As Integer, imageSuffix As String) As String
            Dim format As String = "http://colbertnation.mtvnimages.com/images/shows/colbert_report/videos/season{0}/{1}{2}/cr_{1}{2}{3}.jpg?quality=0.85"
            Return String.Format(format, seasonNumber, seasonNumber.ToString("00"), episodeNumber.ToString("000"), imageSuffix)
        End Function

        Private Function GetLocalNotFoundFileName(seasonNumber As Integer, episodeNumber As Integer) As String
            Dim format As String = "cr_{0}{1}_XNotFound.jpg"
            Return String.Format(format, seasonNumber.ToString("00"), episodeNumber.ToString("000"))
        End Function

    End Class

End Namespace
