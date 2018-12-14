Option Strict On

Imports System.Runtime.Serialization
Imports System.Text.RegularExpressions
Imports Newtonsoft.Json

Namespace ViewModels

    Public Class Channel5ViewModel
        Inherits ViewModelBase

        Private tvData As TvDataSeries

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
            Dim episodes = GetEpisodes()
            tvData = New TvDataSeries()
            For Each ep In episodes
                tvData.Episodes.Add(New TvDataEpisode() With {.EpisodeName = ep.Title,
                                                              .EpisodeNumber = ep.EpisodeNumber,
                                                              .FirstAired = ep.FirstAired,
                                                              .Overview = ep.ShortDescription,
                                                              .SeasonNumber = ep.SeasonNumber.Value})
            Next

            Parallel.ForEach(episodes, AddressOf DownloadImage)
            'For Each episode In episodes
            '    DownloadImage(episode)
            'Next

            If tvData.Episodes.Count > 0 Then
                tvData.SaveToFile(ShowDownloadFolder, ShowName)
            Else
                MessageWindow.ShowDialog("No episodes were found", "No Episodes Found")
            End If
        End Sub

        Private Sub DownloadImage(episode As Channel5Episode)
            Dim extension = IO.Path.GetExtension(episode.LargeImage)
            Dim SeasonEpisodeFilename = "S" & episode.SeasonNumber.Value.ToString("00") & "E" & episode.EpisodeNumber.ToString("00") & "-" & episode.Title.MakeFileNameSafe() & extension
            Dim localPath = IO.Path.Combine(SeasonDownloadFolder(episode.SeasonNumber.Value), SeasonEpisodeFilename.Replace(" ", "_"))
            DownloadImageAddResult(episode.LargeImage, localPath)
        End Sub

        Private Function GetEpisodes() As List(Of Channel5Episode)
            Dim returnList As New List(Of Channel5Episode)
            Dim html As String = String.Empty
            Using webClient As New Infrastructure.ChromeWebClient
                Try
                    html = webClient.DownloadString(ShowUrl)
                Catch ex As Exception
                    MessageWindow.ShowDialog("", "Caption")
                    Return returnList
                End Try
            End Using
            Dim postId = GetRegexMatch(html, "show.post_id\s*=\s*(\d+);")
            Dim catId = GetRegexMatch(html, "show.cat_id\s*=\s*(\d+);")
            Dim apiUrlFormat = "http://www.channel5.com/wp-admin/admin-ajax.php?action=get_show_episodes&data=%7B%22offset%22:{0},%22limit%22:{1},%22show%22:%7B%22post_id%22:{2},%22cat_id%22:{3},%22show_friendly_name%22:%22{4}%22%7D,%22first_time%22:0%7D"
            '                  "http://www.channel5.com/wp-admin/admin-ajax.php?action=get_show_episodes&data=%7B%22offset%22:11,%22limit%22:6,%22show%22:%7B%22post_id%22:19249,%22cat_id%22:721,%22show_friendly_name%22:%22wissper%22%7D,%22first_time%22:0%7D
            '                  "http://www.channel5.com/wp-admin/admin-ajax.php?action=get_show_episodes&data={""offset"":0,""limit"":5,""show"":{""post_id"":19249,""cat_id"":721,""show_friendly_name"":""wissper""},""first_time"":1}"
            ' {0} = offset
            ' {1} = limit
            ' {2} = post_id
            ' {3} = cat_id
            ' {4} = show_friendly_name
            ' {5} = first_time
            Dim offset = 0
            Dim limit = 5
            'Dim firstTime = 1
            Dim episodesPage As Channel5EpisodesPage
            Using webClient As New Infrastructure.ChromeWebClient
                Do
                    Dim jsonUrl = String.Format(apiUrlFormat, offset, limit, postId, catId, ShowName.ToSlug())
                    Dim json = webClient.DownloadString(jsonUrl)
                    episodesPage = JsonConvert.DeserializeObject(Of Channel5EpisodesPage)(json)

                    returnList.AddRange(episodesPage.Episodes.Values)
                    offset = offset + limit
                    limit = 6
                    'firstTime = 0
                Loop Until episodesPage.IsLastPage = True
            End Using
            Return returnList
        End Function

        Private Function GetRegexMatch(input As String, pattern As String) As String
            Dim theMatch = Regex.Match(input, pattern)
            If theMatch.Success Then
                Return theMatch.Groups(1).Value
            End If
            Return Nothing
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

#Region " Data Structures "

        <DataContract>
        Public Class Channel5EpisodesPage

            <JsonProperty("hide_loadmore")>
            Public Property HideLoadmore As Integer

            <JsonProperty("has_data")>
            Public Property HasData As Integer

            <JsonProperty("episodes")>
            Public Property Episodes As Dictionary(Of String, Channel5Episode)

            <IgnoreDataMember>
            Public ReadOnly Property IsLastPage As Boolean
                Get
                    Return HideLoadmore = 1
                End Get
            End Property

        End Class

        <DataContract>
        Public Class Channel5Episode

            <JsonProperty("image")>
            Public Property Image As String

            <IgnoreDataMember>
            Public ReadOnly Property LargeImage As String
                Get
                    Dim returnValue = Regex.Replace(Image, "(\d+x\d+)", "1920x1080")
                    Dim theUri = New Uri(returnValue)
                    Return theUri.GetLeftPart(UriPartial.Path)
                End Get
            End Property

            <JsonProperty("title")>
            Public Property Title As String

            '<JsonProperty("episode_post_id")>
            'Public Property EpisodePostId As String

            <JsonProperty("season_title")>
            Private Property SeasonTitle As String

            <IgnoreDataMember, JsonIgnore>
            Public ReadOnly Property SeasonNumber As Integer?
                Get
                    Dim rx As New Regex("S(\d+)", RegexOptions.IgnoreCase)
                    Dim seasonNoMatch = rx.Match(SeasonTitle)
                    If seasonNoMatch.Success Then
                        Return CInt(seasonNoMatch.Groups(1).Value)
                    End If
                    Return Nothing
                End Get
            End Property

            <JsonProperty("short_description")>
            Public Property ShortDescription As String

            <JsonProperty("episode_number")>
            Private Property EpisodeNumberTitle As String

            <IgnoreDataMember, JsonIgnore>
            Public ReadOnly Property EpisodeNumber As Integer
                Get
                    Dim rx As New Regex("E(\d+)", RegexOptions.IgnoreCase)
                    Dim episodeNoMatch = rx.Match(EpisodeNumberTitle)
                    If episodeNoMatch.Success Then
                        Return CInt(episodeNoMatch.Groups(1).Value)
                    End If
                    Return Nothing
                End Get
            End Property

            <JsonProperty("channels")>
            Public Property Channels As List(Of Channel)

            <IgnoreDataMember, JsonIgnore>
            Public ReadOnly Property FirstAired As String
                Get
                    If Channels.Count > 0 Then
                        Dim dte As Date
                        If Date.TryParse(Channels(0).Transmission, dte) Then
                            Return dte.ToString("yyyy-MM-dd")
                        End If
                    End If
                    Return String.Empty
                End Get
            End Property

        End Class

        <DataContract>
        Public Class Channel

            <JsonProperty("name")>
            Public Property Name As String

            <JsonProperty("id")>
            Public Property Id As Integer

            <JsonProperty("channel_link")>
            Public Property ChannelLink As String

            <JsonProperty("transmission")>
            Public Property Transmission As String
        End Class

#End Region

    End Class

End Namespace
