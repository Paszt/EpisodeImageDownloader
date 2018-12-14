Option Strict On

Imports CsQuery
Imports System.Text.RegularExpressions

Namespace ViewModels

    Public Class HistoryViewModel
        Inherits ViewModels.ViewModelBase


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

        Private _seasonNumber As Integer?
        Public Property SeasonNumber As Integer?
            Get
                Return _seasonNumber
            End Get
            Set(value As Integer?)
                SetProperty(_seasonNumber, value)
            End Set
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

        Public ReadOnly Property SeasonDownloadFolder(SeasonNumber As Integer) As String
            Get
                Return IO.Path.Combine(ShowDownloadFolder,
                                       "Season " & CInt(SeasonNumber).ToString("00"))
            End Get
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName) AndAlso
                   Not String.IsNullOrWhiteSpace(ShowUrl)
        End Function

        Protected Overrides Sub DownloadImages()
            Dim seasons As New List(Of Integer)

            If SeasonNumber.HasValue Then
                seasons.Add(SeasonNumber.Value)
            Else

                Dim html = Infrastructure.WebResources.DownloadString(ShowUrl)
                Dim doc = CQ.CreateDocument(html)
                For Each li In doc("#season-dropdown li")
                    Dim rex = New Regex("season (\d+)", RegexOptions.IgnoreCase)

                    Dim seasonMatch = rex.Match(li.Cq.Text())
                    If seasonMatch.Success Then
                        seasons.Add(CInt(seasonMatch.Groups(1).Value))
                    End If
                Next
            End If

            Dim episodes As New List(Of GenericEpisode)
            For Each sn In seasons
                episodes.AddRange(GetEpisodeInformation(sn))
            Next

            Parallel.ForEach(episodes, AddressOf DownloadEpisodeImage)
            'For Each ep In episodes
            '    DownloadEpisodeImage(ep)
            'Next
        End Sub

        Private Function GetEpisodeInformation(SeasonNo As Integer) As List(Of GenericEpisode)
            Dim eps As New List(Of GenericEpisode)
            Dim html As String = Infrastructure.WebResources.DownloadString(SeasonUrl(SeasonNo))
            Dim doc As CQ = CQ.CreateDocument(html)
            Dim tvData As New TvDataSeries()

            For Each episodeDiv In doc("div.episode-item")
                Dim ep As New GenericEpisode() With {.ImageUrl = episodeDiv.Cq.Find("img").Attr("data-original")},
                    numberRegex As New Regex("(\d+)"),
                    dateRegex As New Regex("(\w{3} \d{2}, \d{4})")
                ' remove resizing queryString
                ep.ImageUrl = ep.ImageUrl.Substring(0, ep.ImageUrl.IndexOf("?"))
                'Season Number
                Dim numberMatch = numberRegex.Match(episodeDiv.Cq.Find(".season-number").Text())
                If numberMatch.Success Then
                    ep.SeasonNumber = CInt(numberMatch.Groups(1).Value)
                End If
                'Episode Number
                numberMatch = numberRegex.Match(episodeDiv.Cq.Find(".episode-number").Text())
                If numberMatch.Success Then
                    ep.EpisodeNumber = CInt(numberMatch.Groups(1).Value)
                End If
                'Title
                ep.Title = episodeDiv.Cq.Find(".episode-name").Text()
                'Description
                ep.Description = episodeDiv.Cq.Find(".description").Text().Trim()

                Dim tvEp As New TvDataEpisode() With {
                                .SeasonNumber = ep.SeasonNumber,
                                .EpisodeNumber = ep.EpisodeNumber,
                                .EpisodeName = ep.Title,
                                .Overview = ep.Description}
                tvData.Episodes.Add(tvEp)
                'Aired Date
                Dim dateTextMatch = dateRegex.Match(episodeDiv.Cq.Find(".episode-airdate").Text())
                If dateTextMatch.Success Then
                    tvEp.FirstAired = dateTextMatch.Groups(1).Value.ToIso8601DateString()
                End If
                eps.Add(ep)
            Next
            If tvData.Episodes.Count > 0 Then
                tvData.SaveToFile(ShowDownloadFolder, ShowName & " " & "Season " & SeasonNo)
            End If
            Return eps
        End Function

        'Private Function GetEpisodeInformation() As List(Of GenericEpisode)
        '    Dim eps As New List(Of GenericEpisode)
        '    Dim html As String = Infrastructure.WebResources.DownloadString(ShowUrl)
        '    Dim doc As CQ = CQ.CreateDocument(html)
        '    Dim tvData As New TvDataSeries()
        '    For Each li In doc("h4:contains('Full Episodes')").Siblings().Find("ul.thumb-list.slider-content li a")
        '        Dim ep As New GenericEpisode()

        '        Dim context = li.Cq.Attr("data-content")
        '        Dim contextFrag = CQ.CreateFragment(context)
        '        ep.Description = contextFrag("p").First().Text()
        '        Dim match = Regex.Match(contextFrag("p").Last().Text, "Season\s+(?<SeasonNumber>\d+),\s+Episode\s(?<EpisodeNumber>\d+)")
        '        If match.Success Then
        '            ep.SeasonNumber = CInt(match.Groups("SeasonNumber").Value)
        '            ep.EpisodeNumber = CInt(match.Groups("EpisodeNumber").Value)

        '            ep.Title = Regex.Replace(li.Cq.Attr("data-original-title"), "[\w\s]+:\s+", String.Empty)
        '            ep.ImageUrl = li.Cq.Find("img.thumb").Attr("src")
        '            If String.IsNullOrWhiteSpace(ep.ImageUrl) Then
        '                ep.ImageUrl = li.Cq.Find("img.thumb").Attr("data-src")
        '            End If
        '            eps.Add(ep)
        '            Dim tde As New TvDataEpisode With {.EpisodeName = ep.Title,
        '                                               .EpisodeNumber = CShort(ep.EpisodeNumber),
        '                                               .SeasonNumber = CShort(ep.SeasonNumber),
        '                                               .Overview = ep.Description}
        '            tvData.Episodes.Add(tde)
        '        End If
        '    Next
        '    If tvData.Episodes.Count > 0 Then
        '        ' save each season in a separate file
        '        For Each seasonNo In (From td In tvData.Episodes
        '                              Select td.SeasonNumber Distinct).ToList()
        '            Dim seasonTvdata As New TvDataSeries
        '            seasonTvdata.Episodes = tvData.Episodes.Where(Function(t) t.SeasonNumber = seasonNo).
        '                                                    OrderBy(Function(t) t.SeasonNumber).
        '                                                    ThenBy(Function(t) t.EpisodeNumber).ToList()
        '            seasonTvdata.SaveToFile(ShowDownloadFolder, ShowName & " " & "Season " & seasonNo)
        '        Next
        '    End If
        '    Return eps
        'End Function

        Private Sub DownloadEpisodeImage(ep As GenericEpisode)
            If String.IsNullOrWhiteSpace(ep.ImageUrl) Then
                AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = "S" & ep.SeasonNumber.ToString("00") & "E" & ep.EpisodeNumber.ToString("00"),
                                                                     .HasError = True, .NewDownload = False,
                                                                     .Message = "Error: No image found!"})
            Else
                Dim Filename = IO.Path.GetFileName(New Uri(ep.ImageUrl).AbsolutePath).Replace(" ", "-")
                Dim SeasonEpisodeFilename = "S" & ep.SeasonNumber.ToString("00") & "E" & ep.EpisodeNumber.ToString("00") & "_" & Filename
                Dim localPath = IO.Path.Combine(SeasonDownloadFolder(ep.SeasonNumber), SeasonEpisodeFilename)
                MyBase.DownloadImageAddResult(ep.ImageUrl, localPath)
            End If
        End Sub

        Private Function SeasonUrl(SeasonNumber As Integer) As String
            Return IO.Path.Combine(ShowUrl, "season-" & SeasonNumber)
        End Function

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


    End Class

End Namespace
