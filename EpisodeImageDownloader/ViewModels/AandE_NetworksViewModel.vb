Imports System.Runtime.CompilerServices
Imports System.Text.RegularExpressions
Imports CsQuery
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    Public Class AandE_NetworksViewModel
        Inherits ViewModelBase

        Private tvData As TvDataSeries
        Private seasonInfos As List(Of SeasonNumberUrlInfo)

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

        Private _showUrl As String
        Public Property ShowUrl As String
            Get
                Return _showUrl
            End Get
            Set(value As String)
                SetProperty(_showUrl, value)
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

            seasonInfos = New List(Of SeasonNumberUrlInfo)
            Dim primaryHtml As String = WebResources.DownloadString(ShowUrl)
            Dim primaryDoc = CQ.CreateDocument(primaryHtml)
            GetCurrentSeason(primaryDoc)
            ParseSeasons(primaryDoc)
            ParseSeasonHtml(primaryHtml)

            For Each seasonInfo In seasonInfos
                If Not String.IsNullOrEmpty(seasonInfo.Url) Then
                    Dim html = WebResources.DownloadString(seasonInfo.Url)
                    ParseSeasonHtml(html)
                End If
            Next

            Parallel.ForEach(tvData.Episodes, AddressOf DownloadImage)

            If tvData.Episodes.Count > 0 Then
                tvData.SaveToFile(ShowDownloadFolder, ShowName)
            Else
                MessageWindow.ShowDialog("No episodes were found", "No Episodes Found")
            End If
        End Sub

        Private Sub ParseSeasonHtml(html As String)
            Dim doc = CQ.CreateDocument(html)
            doc.Find("a.program-item:has(span.episode-number):has(span.season-number)").Each(
                Sub(anchor As IDomObject)
                    Dim a = anchor.Cq
                    Dim ep As New TvDataEpisode() With {
                    .EpisodeName = a.Find("h3.title").Text().Trim(),
                    .EpisodeNumber = CInt(a.Find("span.episode-number").Text().Replace("E", String.Empty).Trim()),
                    .SeasonNumber = CInt(a.Find("span.season-number").Text().Replace("S", String.Empty).Trim()),
                    .ImageUrl = a.Find("img").Attr("data-original").RemoveQuerystring(),
                    .FirstAired = a.Find("strong.date").Text().Replace("Premieres", String.Empty).Replace("Aired", String.Empty).Replace("on", String.Empty).Trim().ToIso8601DateString(),
                    .Overview = Net.WebUtility.HtmlDecode(a.Find("div.description").Text())}
                    tvData.Episodes.Add(ep)
                End Sub)
        End Sub

        Private Sub DownloadImage(episode As TvDataEpisode)
            Dim extension = IO.Path.GetExtension(episode.ImageUrl)
            Dim SeasonEpisodeFilename = "S" & episode.SeasonNumber.ToString("00") & "E" & episode.EpisodeNumber.ToString("00") & "-" & episode.EpisodeName.MakeFileNameSafe() & extension
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(episode.SeasonNumber), SeasonEpisodeFilename.Replace(" ", "_"))
            DownloadImageAddResult(episode.ImageUrl, localPath)
        End Sub

        Private Sub GetCurrentSeason(doc As CQ)
            Dim seasonText = doc.Find("strong.current-season").Text().Trim()
            Dim match = Regex.Match(seasonText, "SEASON (\d+)", RegexOptions.IgnoreCase)
            If match.Success Then
                seasonInfos.Add(New SeasonNumberUrlInfo() With {.Number = CType(match.Groups(1).Value, Integer)})
            End If
        End Sub

        Private Sub ParseSeasons(doc As CQ)
            doc.Find("#season-dropdown li").Each(
                Sub(li As IDomObject)
                    Dim liText = li.Cq.Text()
                    Dim liMatch = Regex.Match(liText, "SEASON (\d+)", RegexOptions.IgnoreCase)
                    If liMatch.Success Then
                        Dim seasonNo = CType(liMatch.Groups(1).Value, Integer)
                        If seasonInfos.Where(Function(si) si.Number.HasValue AndAlso si.Number.Value = seasonNo).Count = 0 Then
                            seasonInfos.Add(New SeasonNumberUrlInfo() With {
                                            .Number = seasonNo,
                                            .Url = EnsureAbsoluteUrl(li.Cq.Attr("data-canonical"))})
                        End If
                    End If
                End Sub)
        End Sub

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

    End Class

End Namespace
