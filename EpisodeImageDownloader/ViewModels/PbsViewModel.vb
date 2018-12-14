Imports System.Text.RegularExpressions
Imports CsQuery
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    Public Class PbsViewModel
        Inherits ViewModelBase

        Private tvData As TvDataSeries
        Private Const seasonUrlFormat = "season/{0}/"

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

        Private _showUrl As String
        Public Property ShowUrl As String
            Get
                Return _showUrl
            End Get
            Set(value As String)
                SetProperty(_showUrl, value)
                Validate()
            End Set
        End Property

        Public ReadOnly Property SeasonDownloadFolder(SeasonNumber As Integer) As String
            Get
                Return IO.Path.Combine(ShowDownloadFolder, "Season " & CInt(SeasonNumber).ToString("00"))
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

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName) AndAlso
                   Not String.IsNullOrWhiteSpace(ShowUrl)
        End Function

        Protected Overrides Sub DownloadImages()
            tvData = New TvDataSeries()

            If SeasonNumber.HasValue Then
                ScrapeSeason(SeasonNumber.Value)
            Else
                Dim availableSeasonNos As New List(Of Integer)
                My.Application.StatusMessage = "Downloading Primary Page"
                Dim primaryHtml = WebResources.DownloadString(ShowUrl)
                Dim primaryDoc = CQ.CreateDocument(primaryHtml)
                availableSeasonNos = primaryDoc("select.content-filter option").Map(Function(s) CInt(s.Value())).ToList()

                My.Application.StatusMessage = "Downloading Season Info"
                If availableSeasonNos.Count = 0 Then
                    ScrapeSeason(Nothing)
                End If
                Parallel.ForEach(availableSeasonNos, AddressOf ScrapeSeason)
                'For Each availableSeasonNo As Integer In availableSeasonNos
                '    ScrapeSeason(availableSeasonNo)
                'Next
            End If

            My.Application.StatusMessage = "Downloading Images"
            Parallel.ForEach(tvData.Episodes, AddressOf DownloadImage)

            If tvData.Episodes.Count > 0 Then
                ' save each season in a separate file
                For Each seasonNo In tvData.SeasonNumbersDistinct
                    Dim seasonTvdata As New TvDataSeries With {
                        .Episodes = tvData.Episodes.Where(Function(t) t.SeasonNumber = seasonNo).
                                                            OrderBy(Function(t) t.SeasonNumber).
                                                            ThenBy(Function(t) t.EpisodeNumber).ToList()
                    }
                    seasonTvdata.SeriesInfo.SeriesName = ShowName
                    seasonTvdata.SaveToFile(ShowDownloadFolder, ShowName & " " & "Season " & seasonNo)
                Next
            Else
                MessageWindow.ShowDialog("No episodes were found", "No Episodes Found")
            End If
            My.Application.StatusMessage = String.Empty
        End Sub

        Private Sub ScrapeSeason(seasonNo As Integer?)
            Dim pageQuery As String = String.Empty
            Dim seasonUri = GetSeasonUri(seasonNo)
            Do
                Dim html = WebResources.DownloadString(New Uri(seasonUri, pageQuery))
                pageQuery = ParseSeasonHtml(html)
            Loop While Not String.IsNullOrEmpty(pageQuery)
        End Sub

        Private Function GetSeasonUri(seasonNo As Integer?) As Uri
            If seasonNo.HasValue Then
                'http://www.pbs.org/show/frontline/episodes/season/{0}/
                Return New Uri(String.Format(IO.Path.Combine(ShowUrl, seasonUrlFormat), seasonNo.Value))
            Else
                Return New Uri(ShowUrl)
            End If
        End Function

        Private Function ParseSeasonHtml(html As String) As String
            Dim episodeListDoc = CQ.CreateDocument(html)
            Dim seasonTitle = episodeListDoc.Find("h1.video-catalog__title").Text().Trim()
            Dim episodeDivs = episodeListDoc.Find(".video-catalog__item")
            episodeDivs.Each(
                Sub(zeroBasedIndex As Integer, div As IDomObject)
                    My.Application.StatusMessage = String.Format("Scraping {0} Episode {1} of {2}", seasonTitle, zeroBasedIndex + 1, episodeDivs.Count)
                    Dim divCq = div.Cq
                    Dim ep As New TvDataEpisode() With {
                        .EpisodeName = divCq.Find("h3.video-summary__title").Text().Trim(),
                        .ImageUrl = GetImageUrl(divCq.Find("img").Attr("data-srcset"))
                    }
                    'season & episode numbers
                    Dim metaData = divCq.Find(".popover__meta-data").Text().Trim()
                    ' S01E01, S01E02, etc
                    Dim metaDataMatch = Regex.Match(metaData, "S(\d+)\s+Ep(\d+)", RegexOptions.IgnoreCase)
                    If Not metaDataMatch.Success Then
                        ' Ep101, Ep102, etc
                        metaDataMatch = Regex.Match(metaData, "Ep(\d+)(\d{2})", RegexOptions.IgnoreCase)
                    End If
                    If metaDataMatch.Success Then
                        ep.SeasonNumber = CInt(metaDataMatch.Groups(1).Value)
                        ep.EpisodeNumber = CInt(metaDataMatch.Groups(2).Value)
                        'must scrape episode page to get overview and aired date
                        Dim episodeHtml = WebResources.DownloadString(EnsureAbsoluteUrl(divCq.Find("h3.video-summary__title a").Attr("href")))
                        Dim episodeDoc = CQ.CreateDocument(episodeHtml)
                        'overview
                        ep.Overview = episodeDoc.Find(".video-player__description").Text().Trim()
                        'FirstAired
                        Dim airedMetric = episodeDoc.Find(".video-player__metric:contains(Air)").Text().Trim()
                        Dim airedMetricMatch = Regex.Match(airedMetric, "Air(?:ed|ing):\s+(.+)\s*")
                        If airedMetricMatch.Success Then
                            ep.FirstAired = airedMetricMatch.Groups(1).Value.ToIso8601DateString
                        End If
                        tvData.Episodes.Add(ep)
                    End If
                End Sub)
            Return episodeListDoc.Find(".pagination__univ-feature a:contains(Next)").Attr("href")
        End Function

        Private Sub DownloadImage(episode As TvDataEpisode)
            Dim extension = IO.Path.GetExtension(episode.ImageUrl)
            Dim SeasonEpisodeFilename = "S" & episode.SeasonNumber.ToString("00") & "E" & episode.EpisodeNumber.ToString("00") & "-" & episode.EpisodeName.MakeFileNameSafe() & extension
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(episode.SeasonNumber), SeasonEpisodeFilename.Replace(" ", "_"))
            DownloadImageAddResult(episode.ImageUrl, localPath)
        End Sub

        Private Function GetImageUrl(dataSrcSet As String) As String
            Dim imgUrls = Regex.Replace(dataSrcSet, "\s+[0-9]+(\.[0-9]+)?[wx]", String.Empty).Split(","c)
            If imgUrls.Length > 0 Then
                Dim imgUrl = imgUrls(0)
                Dim match = Regex.Match(imgUrl, "(.+\.(?:png|jpg))\.")
                If match.Success Then
                    Return match.Groups(1).Value
                Else
                    Return Nothing
                End If
            End If
            Return Nothing
        End Function

        Private Function EnsureAbsoluteUrl(Url As String) As String
            Dim absUri = New Uri(Url, UriKind.RelativeOrAbsolute)
            If Not absUri.IsAbsoluteUri Then
                Dim baseUrl = New Uri(ShowUrl).GetLeftPart(UriPartial.Authority)
                absUri = New Uri(baseUrl & Url)
            End If
            Return absUri.ToString()
        End Function

#Region " Folder Methods "

        Public Function ShowDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(ShowDownloadFolder)
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New RelayCommand(Sub()
                                            Process.Start(ShowDownloadFolder)
                                        End Sub, AddressOf ShowDownloadFolderExists)
            End Get
        End Property

#End Region

        Protected Overrides Sub Validate(<Runtime.CompilerServices.CallerMemberName> Optional propertyName As String = Nothing)
            If String.IsNullOrEmpty(propertyName) Then
                Return
            End If

            Select Case propertyName
                Case "ShowUrl"
                    ClearError(propertyName)
                    Dim errMsgBuilder As New Text.StringBuilder()
                    Dim showUri As Uri = Nothing
                    If Not Uri.TryCreate(ShowUrl, UriKind.Absolute, showUri) Then
                        errMsgBuilder.Append("Not a valid URL")
                    End If

                    If Not ShowUrl.EndsWith("/episodes/") And Not ShowUrl.EndsWith("/episodes") Then
                        errMsgBuilder.Append(Environment.NewLine & "Should end with '/episodes/'")
                    End If

                    If Not String.IsNullOrEmpty(errMsgBuilder.ToString) Then
                        AddError(propertyName, errMsgBuilder.ToString)
                    End If
            End Select
        End Sub

    End Class

End Namespace
