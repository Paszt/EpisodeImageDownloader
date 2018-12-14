Imports System.Text.RegularExpressions
Imports CsQuery
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    Public Class NprTinyDeskViewModel
        Inherits ViewModelBase

        Const ShowName As String = "NPR Tiny Desk Concerts"

        Private TvData As TvDataSeries

        Public Sub New()
            LowestYear = Date.Now.Year
        End Sub

#Region " Properties "

        Private _lowestYear As Integer?

        Public Property LowestYear As Integer?
            Get
                Return _lowestYear
            End Get
            Set(value As Integer?)
                SetProperty(_lowestYear, value)
            End Set
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return True
        End Function

        Protected Overrides Sub DownloadImages()
            TvData = New TvDataSeries()
            Dim html As String = String.Empty
            Dim episodeCount As Integer
            'Try
            '    html = WebResources.DownloadString(Url)
            'Catch ex As Exception
            '    MessageWindow.ShowDialog("Error downloading primary HTML: " & ex.Message, "Error")
            '    Exit Sub
            'End Try
            'ParseHtml(html)

            Do
                episodeCount = tvdata.Episodes.Count
                Try
                    ''Console.WriteLine(GetUrl())
                    html = WebResources.DownloadString(GetUrl())
                Catch ex As Exception
                    MessageWindow.ShowDialog("Error downloading partials HTML: " & ex.Message, "Error")
                    Exit Sub
                End Try
                ParseHtml(html)
            Loop Until episodeCount = tvdata.Episodes.Count 'loop until no new episodes are added

            ' sort episodes by FirstAired date and assign EpisodeNumber
            For Each seasonNo In tvdata.SeasonNumbersDistinct
                Dim sortedEpisodes = From e In tvdata.Episodes
                                     Where e.SeasonNumber = seasonNo
                                     Order By e.FirstAired
                Dim counter As Integer = 1
                For Each se In sortedEpisodes
                    se.EpisodeNumber = counter
                    counter += 1
                Next
            Next

            Parallel.ForEach(TvData.Episodes, AddressOf DownloadImage)

            If tvdata.Episodes.Count > 0 Then
                tvdata.SaveToFile(ShowDownloadFolder, ShowName)
            Else
                MessageWindow.ShowDialog("No episodes were found", "No Episodes Found")
            End If
        End Sub

        Private Sub ParseHtml(html As String)
            Dim doc = CQ.CreateDocument(html)
            For Each article In doc.Find("article.article-type-video, article.event-more-article")
                Dim ep As New TvDataEpisode() With {
                    .EpisodeName = article.Cq.Find("h1.title").Text(),
                    .ImageUrl = article.Cq.Find("img").Attr("src"),
                    .Overview = article.Cq.Find("p.teaser").Text().Trim(),
                    .FirstAired = article.Cq.Find(".teaser time").Attr("datetime")}
                If Not String.IsNullOrEmpty(ep.ImageUrl) Then
                    ep.ImageUrl = Regex.Replace(ep.ImageUrl, "-c\d+", String.Empty)
                    ep.ImageUrl = Regex.Replace(ep.ImageUrl, "-s\d+", String.Empty)
                End If
                ep.Overview = ep.Overview.Replace(article.Cq.Find("p.teaser time").Text(), String.Empty).Trim()
                Dim dte As Date
                If Date.TryParse(ep.FirstAired, dte) Then
                    'ep.FirstAired = dte.ToIso8601DateString
                    ep.SeasonNumber = dte.Year
                End If
                If ep.SeasonNumber < LowestYear Then
                    Exit Sub
                Else
                    TvData.Episodes.Add(ep)
                End If
            Next
        End Sub

        Private Sub DownloadImage(episode As TvDataEpisode)
            Dim SeasonEpisodeName = "S" & episode.SeasonNumber.ToString("00") & "E" & episode.EpisodeNumber.ToString("00") & "-" &
                episode.EpisodeName.MakeFileNameSafe()
            If String.IsNullOrEmpty(episode.ImageUrl) Then
                AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeName, .HasError = True, .NewDownload = False, .Message = "No image available"})
                Exit Sub
            End If
            Dim extension = IO.Path.GetExtension(episode.ImageUrl)
            Dim SeasonEpisodeFilename = SeasonEpisodeName & "_" & extension
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(episode.SeasonNumber), SeasonEpisodeFilename.Replace(" ", "_"))
            DownloadImageAddResult(episode.ImageUrl, localPath)
        End Sub

        Private Function GetUrl() As String
            Return "http://www.npr.org/partials/music/series/tiny-desk-concerts/archive?isArchivePage=true&start=" & (TvData.Episodes.Count + 1)
        End Function

#Region " Folder Methods "

        Private Function ShowDownloadFolder() As String
            Return IO.Path.Combine(My.Settings.DownloadFolder, ShowName.MakeFileNameSafe)
        End Function

        Private Function SeasonDownloadFolder(SeasonNumber As Integer) As String
            Return IO.Path.Combine(My.Settings.DownloadFolder,
                                   ShowName.MakeFileNameSafe,
                                   "Season " & SeasonNumber.ToString("00"))
        End Function

        Public Function ShowDownloadFolderExists() As Boolean
            Return IO.Directory.Exists(ShowDownloadFolder)
        End Function

        Public ReadOnly Property OpenFolderCommand As ICommand
            Get
                Return New RelayCommand(Sub()
                                            Process.Start(ShowDownloadFolder)
                                        End Sub,
                                        AddressOf ShowDownloadFolderExists)
            End Get
        End Property

#End Region

    End Class

End Namespace
