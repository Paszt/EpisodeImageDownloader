Imports System.Runtime.Serialization
Imports System.Text.RegularExpressions
Imports CsQuery
Imports EpisodeImageDownloader.Infrastructure


Namespace ViewModels

    Public Class BravoViewModel
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
            Return Not String.IsNullOrWhiteSpace(ShowName)
        End Function

        Protected Overrides Sub DownloadImages()
            Dim episodesBag As New Concurrent.ConcurrentBag(Of TvDataEpisode)

            Dim mainHtml As String
            Try
                mainHtml = WebResources.DownloadString(ShowUrl())
            Catch ex As Exception
                MessageWindow.ShowDialog("Error scraping main page:" & ex.Message, "Error!")
                Exit Sub
            End Try

            Dim seasonDict = GetSeasonIds(mainHtml)
            If seasonDict.Count = 0 Then
                'There is no season selector (dropdown/combobox) so there's only one season
                If SeasonNumber.HasValue AndAlso SeasonNumber.Value <> 1 Then
                    MessageWindow.ShowDialog("There appears to be only the first season on the website, but you've entered a Season Number of " & SeasonNumber.Value, "Season Number mismatch")
                    Exit Sub
                Else
                    ScrapeEpisodes(mainHtml, episodesBag, 1)
                End If
            Else
                If SeasonNumber.HasValue Then
                    ScrapeEpisodes(episodesBag, SeasonNumber.Value, seasonDict(SeasonNumber.Value))
                Else
                    For Each season In seasonDict
                        ScrapeEpisodes(episodesBag, season.Key, season.Value)
                    Next
                End If
            End If

            If episodesBag.Count > 0 Then
                For Each seasonNo In (From eb In episodesBag Select eb.SeasonNumber Distinct)
                    Dim tvd As New TvDataSeries With {
                        .Episodes = episodesBag.Where(Function(e) e.SeasonNumber = seasonNo).OrderBy(Function(e) e.EpisodeNumber).ToList()}
                    tvd.SaveToFile(ShowDownloadFolder, ShowName & " Season " & seasonNo.ToString("00"))
                Next
            Else
                MessageWindow.ShowDialog("No episodes were found in HTML", "No Episodes Found")
            End If
        End Sub

        Private Function GetSeasonIds(html As String) As Dictionary(Of Integer, Integer)
            Dim dic As New Dictionary(Of Integer, Integer)
            Dim doc = CQ.CreateDocument(html)
            doc("#edit-season option").Each(
                Sub(selectOption As IDomObject)
                    Dim seasonNo As Integer = CInt(selectOption.Cq.Text().Replace("Season", String.Empty).Trim())
                    Dim value As Integer = CInt(selectOption.Cq.Attr("value"))
                    dic.Add(seasonNo, value)
                End Sub)
            Return dic
        End Function

        Private Overloads Sub ScrapeEpisodes(episodesBag As Concurrent.ConcurrentBag(Of TvDataEpisode), seasonNo As Integer, seasonId As Integer)
            Dim seasonUrl As String = ShowUrl() & "?season=" & seasonId & "&page=0"
            Dim html As String = WebResources.DownloadString(seasonUrl)
            ScrapeEpisodes(html, episodesBag, seasonNo)
        End Sub

        Private Overloads Sub ScrapeEpisodes(html As String,
                                             episodesBag As Concurrent.ConcurrentBag(Of TvDataEpisode),
                                             seasonNo As Integer)
            Dim doc = CQ.CreateDocument(html)
            doc("article.teaser--episode-guide-teaser").Each(
                Sub(epDiv As IDomObject)
                    Dim epTitle = epDiv.Cq.Find("h2.headline").Text()
                    Dim epNoMatch = Regex.Match(epTitle, "Ep (\d+):", RegexOptions.IgnoreCase)
                    If epNoMatch.Success Then
                        Dim epUrl = EnsureAbsoluteUrl(epDiv.Cq.Find("a").Attr("href"))
                        Dim tvEp = ScrapeEpisode(epUrl)
                        If tvEp IsNot Nothing Then
                            tvEp.SeasonNumber = seasonNo
                            episodesBag.Add(tvEp)
                            DownloadImageAddResult(tvEp.ImageUrl, LocalEpisodeImagePath(tvEp))
                        End If
                    End If
                End Sub)
            'if there's a laod more button, get the next batch of episodes
            If doc("li.pager-next").Length > 0 Then
                Dim nextPageUrl = EnsureAbsoluteUrl(doc("li.pager-next a").Attr("href"))
                If Not String.IsNullOrWhiteSpace(nextPageUrl) Then
                    Dim nextPageHtml = WebResources.DownloadString(nextPageUrl)
                    ScrapeEpisodes(nextPageHtml, episodesBag, seasonNo)
                End If
            End If
        End Sub

        Private Function ScrapeEpisode(url As String) As TvDataEpisode
            Dim html = WebResources.DownloadString(url)
            Dim doc = CQ.CreateDocument(html)
            Dim CqEpContiainer = doc("article.tv-episode")
            Dim titleMatch = Regex.Match(CqEpContiainer.Find("h1.headline").Text(), "Ep\s?(\d+):\s?(.+)", RegexOptions.IgnoreCase)
            If titleMatch.Success Then
                Dim tvEp As New TvDataEpisode() With {
                    .EpisodeName = titleMatch.Groups(2).Value,
                    .EpisodeNumber = CInt(titleMatch.Groups(1).Value),
                    .ImageUrl = EnsureAbsoluteUrl(GetLargeImage(CqEpContiainer.Find("img:first").Attr("src"))),
                    .Overview = CqEpContiainer.Find("div.tv-episode__description").Text()}
                Return tvEp
            End If
            Return Nothing
        End Function

        Private Function EnsureAbsoluteUrl(url As String) As String
            Dim theUri As Uri = Nothing
            If Uri.TryCreate(url, UriKind.Absolute, theUri) Then
                Return theUri.ToString()
            Else
                If Uri.TryCreate(New Uri("http://www.bravotv.com"), url, theUri) Then
                    Return theUri.ToString()
                End If
            End If
            Return Nothing
        End Function

        Private Function GetLargeImage(url As String) As String
            Return Regex.Replace(url, "styles.+\/public\/", String.Empty, RegexOptions.IgnoreCase)
        End Function

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
                Return New RelayCommand(
                    Sub()
                        Process.Start(ShowDownloadFolder())
                    End Sub,
                    AddressOf ShowDownloadFolderExists)
            End Get
        End Property

        Private Function SeasonDownloadFolder(seasonNo As Integer) As String
            Return IO.Path.Combine(ShowDownloadFolder,
                                   "Season " & seasonNo.ToString("00"))
        End Function

#End Region

#Region " Helper Functions "

        Private Function ShowUrl() As String
            Dim urlFormat = "http://www.bravotv.com/{0}/episode-guide"
            Return String.Format(urlFormat, ShowName.ToSlug())
        End Function

        Private Function LocalEpisodeImagePath(tvEp As TvDataEpisode) As String
            Return IO.Path.Combine(SeasonDownloadFolder(tvEp.SeasonNumber),
                                   SeasonEpisodeString(tvEp.SeasonNumber, tvEp.EpisodeNumber) &
                                   "_" & tvEp.EpisodeName.MakeFileNameSafeNoSpaces() & ".jpg")
        End Function

        Private Function SeasonEpisodeString(seasonNo As Integer, episodeNumber As Integer) As String
            Return "S" & seasonNo.ToString("00") & "E" & episodeNumber.ToString("00")
        End Function

        'Private Function BravoAjaxUrl(seasonId As Integer, viewArgs As Integer, viewDomId As String) As String
        '    Dim format = "http://www.bravotv.com/views/ajax?season={0}&view_name=episodes&view_display_id=episodes&view_args={1}&view_path=node/{1}/episode-guide&view_base_path=node/%/episode-guide&view_dom_id={2}&pager_element=0"
        '    Return String.Format(format, seasonId, viewDomId)
        'End Function

#End Region

        '#Region " Data Structures "

        '        <DataContract>
        '        Public Class BravoInfo

        '            <DataMember(Name:="command")>
        '            Public Property Command As String

        '            <DataMember(Name:="merge")>
        '            Public Property Merge As Boolean

        '            <DataMember(Name:="selector")>
        '            Public Property Selector As String

        '            <DataMember(Name:="method")>
        '            Public Property Method As String

        '            <DataMember(Name:="data")>
        '            Public Property Data As String

        '        End Class

        '#End Region

    End Class

End Namespace
