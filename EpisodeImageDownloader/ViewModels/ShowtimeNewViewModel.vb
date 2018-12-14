Option Strict On

Imports System.Text.RegularExpressions
Imports CsQuery
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    Public Class ShowtimeNewViewModel
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

        Private _ImageSize As String = "1920x1080"
        Public Property ImageSize As String
            Get
                Return _ImageSize
            End Get
            Set(value As String)
                SetProperty(_ImageSize, value)
            End Set
        End Property

        Public Shared ReadOnly Property ImageSizes As List(Of String)
            Get
                Return New List(Of String)(New String() {"1920x1080", "1024x640", "800x600"})
            End Get
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName)
        End Function

        Protected Overrides Sub DownloadImages()
            Dim episodesBag As New Concurrent.ConcurrentBag(Of TvDataEpisode)

            If SeasonNumber.HasValue Then
                DownloadSeasonImages(SeasonNumber.Value, episodesBag)
            Else
                'get number of seasons from main page
                Dim mainHtml = String.Empty
                Dim seasonNumbers As New List(Of Integer)
                Dim seriesUrl = ShowUrl()
                Try
                    mainHtml = WebResources.DownloadString(seriesUrl)
                Catch ex As Exception
                    MessageWindow.ShowDialog("Error downloading show's main page: " & seriesUrl, "Error getting number of seasons")
                    NotBusy = True
                    Exit Sub
                End Try
                Dim mainDoc = CQ.CreateDocument(mainHtml)
                Dim seasonListItems = mainDoc(".slider--season ul li")
                For Each seasonListItem In seasonListItems
                    Dim seasonRegEx As New Regex("season (\d+)", RegexOptions.IgnoreCase)
                    Dim seasonMatch = seasonRegEx.Match(seasonListItem.Cq().Find("a.promo__link").Text())
                    If seasonMatch.Success Then
                        seasonNumbers.Add(CInt(seasonMatch.Groups(1).Value))
                    End If
                Next
                'For Each seasonNo In seasonNumbers.OrderBy(Function(sn) sn)
                '    DownloadSeasonImages(seasonNo, episodesBag)
                'Next
                Parallel.ForEach(seasonNumbers,
                                 Sub(seasonNo As Integer)
                                     DownloadSeasonImages(seasonNo, episodesBag)
                                 End Sub)
            End If

            If episodesBag.Count > 0 Then
                Dim tvdata As New TvDataSeries With {
                    .Episodes = episodesBag.OrderBy(Function(e) e.SeasonNumber).ThenBy(Function(e) e.EpisodeNumber).ToList()
                }
                Dim fileSuffix = String.Empty
                If SeasonNumber.HasValue Then
                    fileSuffix = " " & "Season " & SeasonNumber.Value.ToString("00")
                End If
                tvdata.SaveToFile(ShowDownloadFolder, ShowName & fileSuffix)
            Else
                MessageWindow.ShowDialog("No episodes were found in HTML", "No Episodes Found")
            End If

        End Sub

        Private Sub DownloadSeasonImages(seasonNo As Integer, episodesBag As Concurrent.ConcurrentBag(Of TvDataEpisode))
            Dim episodeNumber As Integer = 1
            Do
                Dim epUrl = EpisodeUrl(seasonNo, episodeNumber)
                Dim epHtml = String.Empty
                Try
                    epHtml = WebResources.DownloadString(epUrl)
                Catch ex As Exception
                    Exit Do
                End Try
                Dim epDoc = CQ.CreateDocument(epHtml)
                Dim epImgDataBgSet = epDoc(".hero__image").Attr("data-bgset").Split("|"c)
                If epImgDataBgSet.Length > 1 Then
                    Dim epImgSrc = epImgDataBgSet(1)
                    If Not epImgSrc.Contains("0_0_0_00") AndAlso Not epImgSrc.Contains("0_0_0_01") Then
                        DownloadEpisodeImages(seasonNo, episodeNumber, epImgSrc)
                    End If
                End If

                Dim ep As New TvDataEpisode() With {
                    .EpisodeName = epDoc("h1.hero__headline").Text().Trim(),
                    .Overview = epDoc(".hero__description").Text().Trim(),
                    .FirstAired = epDoc(".hero__subtitle").Text().Replace("Original Air Date: ", "").ToIso8601DateString(),
                    .SeasonNumber = seasonNo,
                    .EpisodeNumber = episodeNumber
                }
                episodesBag.Add(ep)

                episodeNumber += 1
            Loop
        End Sub

        Private Sub DownloadEpisodeImages(seasonNo As Integer, episodeNumber As Integer, epImgSrc As String)
            Dim imgSrcBase = Regex.Replace(epImgSrc, "\d{2}_(\d+x\d+).jpg", String.Empty)
            For imageNumber As Integer = 1 To 30
                Dim imgUrl = imgSrcBase & imageNumber.ToString("00") & "_" & ImageSize & ".jpg"
                DownloadImageAddResult(imgUrl, LocalEpisodeImagePath(seasonNo, episodeNumber, imageNumber))
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
                    End Sub,
                    AddressOf ShowDownloadFolderExists)
            End Get
        End Property

        'Private Function SeasonDownloadFolder() As String
        '    Return IO.Path.Combine(ShowDownloadFolder,
        '                           "Season " & CInt(SeasonNumber).ToString("00"))
        'End Function

        Private Function SeasonDownloadFolder(seasonNo As Integer) As String
            Return IO.Path.Combine(ShowDownloadFolder,
                                   "Season " & seasonNo.ToString("00"))
        End Function

        'Private Function EpisodeDownloadFolder(episodeNumber As Integer) As String
        '    Return IO.Path.Combine(SeasonDownloadFolder,
        '                           SeasonEpisodeString(episodeNumber))
        'End Function

        Private Function EpisodeDownloadFolder(seasonNo As Integer, episodeNumber As Integer) As String
            Return IO.Path.Combine(SeasonDownloadFolder(seasonNo),
                                   SeasonEpisodeString(seasonNo, episodeNumber))
        End Function

#End Region

#Region " Helper Functions "

        Private Function ShowUrl() As String
            Dim urlFormat = "http://www.sho.com/{0}"
            Return String.Format(urlFormat, ShowName.ToSlug())
        End Function

        Private Function EpisodeUrl(seasonNumber As Integer, episodeNumber As Integer) As String
            Dim urlFormat = "http://www.sho.com/{0}/season/{1}/episode/{2}"
            Return String.Format(urlFormat, ShowName.ToSlug(), seasonNumber, episodeNumber)
        End Function

        'Private Function LocalEpisodeImagePath(episodeNumber As Integer, imageNumber As Integer) As String
        '    Return IO.Path.Combine(EpisodeDownloadFolder(episodeNumber), SeasonEpisodeString(episodeNumber) & "_" & imageNumber.ToString("00") & ".jpg")
        'End Function

        Private Function LocalEpisodeImagePath(seasonNo As Integer, episodeNumber As Integer, imageNumber As Integer) As String
            Return IO.Path.Combine(EpisodeDownloadFolder(seasonNo, episodeNumber), SeasonEpisodeString(seasonNo, episodeNumber) & "_" & imageNumber.ToString("00") & ".jpg")
        End Function

        'Private Function SeasonEpisodeString(episodeNumber As Integer) As String
        '    Return "S" & SeasonNumber.Value.ToString("00") & "E" & episodeNumber.ToString("00")
        'End Function

        Private Function SeasonEpisodeString(seasonNo As Integer, episodeNumber As Integer) As String
            Return "S" & seasonNo.ToString("00") & "E" & episodeNumber.ToString("00")
        End Function

#End Region

    End Class

End Namespace
