Imports CsQuery
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    Public Class ShowtimeViewModel
        Inherits ViewModelBase

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

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName) AndAlso SeasonNumber.HasValue
        End Function

        Protected Overrides Sub DownloadImages()
            Dim tvdata As New TvDataSeries
            Dim episodeNumber As Integer = 1
            Do
                Dim epUrl = EpisodeUrl(SeasonNumber.Value, episodeNumber)
                Dim epHtml = String.Empty
                Try
                    epHtml = WebResources.DownloadString(epUrl)
                Catch ex As Exception
                    Exit Do
                End Try
                Dim epDoc = CQ.CreateDocument(epHtml)
                Dim epImgSrc = epDoc(".product-art img").Attr("src")

                If Not epImgSrc.Contains("0_0_0_00") Then
                    DownloadEpisodeImages(episodeNumber, epImgSrc)
                End If

                Dim ep As New TvDataEpisode() With {
                    .EpisodeName = epDoc("h2[itemprop=name]").Text(),
                    .Overview = epDoc("p[itemprop=description]").Text(),
                    .SeasonNumber = SeasonNumber.Value,
                    .EpisodeNumber = episodeNumber
                }
                tvdata.Episodes.Add(ep)

                episodeNumber += 1
            Loop


            If tvdata.Episodes.Count > 0 Then
                tvdata.SaveToFile(ShowDownloadFolder, ShowName & " " & "Season " & SeasonNumber.Value.ToString("00"))
            Else
                MessageWindow.ShowDialog("No episodes were found in HTML", "No Episodes Found")
            End If
        End Sub

        Private Sub DownloadEpisodeImages(episodeNumber As Integer, epImgSrc As String)
            Dim imgSrcBase = epImgSrc.Replace("01_444x250.jpg", String.Empty).Replace("00_444x250.jpg", String.Empty)
            For imageNumber As Integer = 1 To 30
                Dim imgUrl = imgSrcBase & imageNumber.ToString("00") & "_1920x1080.jpg"
                MyBase.DownloadImageAddResult(imgUrl, LocalEpisodeImagePath(episodeNumber, imageNumber))
            Next
        End Sub

#Region " Folder Functions "

        Public Function ShowDownloadFolder() As String
            If Not String.IsNullOrWhiteSpace(ShowName) Then
                Return IO.Path.Combine(My.Settings.DownloadFolder, ShowName.MakeFileNameSafe())
            Else
                Return String.Empty
            End If
        End Function

        Public Function ShowDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(ShowDownloadFolder())
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New Infrastructure.RelayCommand(
                    Sub()
                        Process.Start(ShowDownloadFolder())
                    End Sub, AddressOf ShowDownloadFolderExists)
            End Get
        End Property

        Private Function SeasonDownloadFolder() As String
            Return IO.Path.Combine(ShowDownloadFolder,
                                   "Season " & CInt(SeasonNumber).ToString("00"))
        End Function

        Private Function EpisodeDownloadFolder(episodeNumber As Integer) As String
            Return IO.Path.Combine(SeasonDownloadFolder,
                                   SeasonEpisodeString(episodeNumber))
        End Function

#End Region

#Region " Helper Functions "

        Private Function EpisodeUrl(seasonNumber As Integer, episodeNumber As Integer) As String
            Dim urlFormat = "http://www.sho.com/sho/{0}/season/{1}/episode/{2}"
            Return String.Format(urlFormat, ShowName.ToLower().Replace(" ", "-"), seasonNumber, episodeNumber)
        End Function

        Private Function LocalEpisodeImagePath(episodeNumber As Integer, imageNumber As Integer) As String
            Return IO.Path.Combine(EpisodeDownloadFolder(episodeNumber), SeasonEpisodeString(episodeNumber) & "_" & imageNumber.ToString("00") & ".jpg")
        End Function

        Private Function SeasonEpisodeString(episodeNumber As Integer) As String
            Return "S" & SeasonNumber.Value.ToString("00") & "E" & episodeNumber.ToString("00")
        End Function

#End Region

    End Class

End Namespace
