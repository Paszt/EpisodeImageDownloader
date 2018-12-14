Namespace ViewModels

    Public Class TheOppositionViewModel
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
            If Not IO.Directory.Exists(DownloadFolder) Then
                IO.Directory.CreateDirectory(DownloadFolder)
            End If

            Using wc As New Infrastructure.ChromeWebClient()
                For episodeNumber As Integer = CInt(EpisodeFrom) To CInt(EpisodeTo) ' <--- episode numbers
                    DownloadImage(episodeNumber, String.Empty, wc)
                    For j As Integer = 1 To 5
                        DownloadImage(episodeNumber, "_act" & j.ToString(), wc)
                    Next
                    For imageNumber = 1 To 8
                        DownloadImage(episodeNumber, "_" & imageNumber.ToString("00"), wc)
                    Next
                Next
            End Using
        End Sub

        Private Sub DownloadImage(episodeNumber As Integer, imageSuffix As String, wc As Infrastructure.ChromeWebClient)
            Dim localNotFoundFilename As String = GetLocalNotFoundFileName(CInt(SeasonNumber), episodeNumber)
            Dim url = GetImageUrl(CInt(SeasonNumber), episodeNumber, imageSuffix)
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
            If SeasonNumber.HasValue Then
                Return IO.Path.Combine(My.Settings.DownloadFolder, "The Opposition with Jordan Klepper", "Season " & SeasonNumber.Value.ToString("00"))
            Else
                Return String.Empty
            End If
        End Function

        Public Function DownloadFolderExists() As Boolean
            Return IO.Directory.Exists(DownloadFolder)
        End Function

        Private Function GetImageUrl(seasonNumber As Integer, episodeNumber As Integer, imageSuffix As String) As String
            '                       https://comedycentral.mtvnimages.com/images/shows/The_Opposition_with_Jordan_Klepper/Videos/Season01/01007/the_opposition_01_007_act1.jpg
            Dim format As String = "https://comedycentral.mtvnimages.com/images/shows/The_Opposition_with_Jordan_Klepper/Videos/Season{0}/{0}{1}/the_opposition_{0}_{1}{2}.jpg"
            Return String.Format(format, seasonNumber.ToString("00"), episodeNumber.ToString("000"), imageSuffix)
        End Function

        Private Function GetLocalNotFoundFileName(seasonNumber As Integer, episodeNumber As Integer) As String
            Dim format As String = "the_opposition_{0}_{1}_XNotFound.jpg"
            Return String.Format(format, seasonNumber.ToString("00"), episodeNumber.ToString("000"))
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(
                    Sub()
                        Process.Start(DownloadFolder)
                    End Sub,
                    AddressOf DownloadFolderExists)
            End Get
        End Property

    End Class

End Namespace
