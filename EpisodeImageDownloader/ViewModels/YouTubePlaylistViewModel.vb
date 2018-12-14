Imports System.Runtime.Serialization
Imports System.Text
Imports EpisodeImageDownloader.Infrastructure

Namespace ViewModels

    Public Class YouTubePlaylistViewModel
        Inherits ViewModelBase

        'Tiny Desk playlistId = PL1B627337ED6F55F0

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

        Private _playlistId As String
        Public Property PlaylistId As String
            Get
                Return _playlistId
            End Get
            Set(value As String)
                SetProperty(_playlistId, value)
            End Set
        End Property

        Private _retrievalMode As YouTubeRetrievalMode = YouTubeRetrievalMode.ByYear
        Public Property RetrievalMode As YouTubeRetrievalMode
            Get
                Return _retrievalMode
            End Get
            Set(value As YouTubeRetrievalMode)
                If SetProperty(_retrievalMode, value) Then
                    IsSeasonRelevant = Not (value = YouTubeRetrievalMode.ByYear)
                End If
            End Set
        End Property

        Private _isSeasonRelevant As Boolean
        Public Property IsSeasonRelevant As Boolean
            Get
                Return _isSeasonRelevant
            End Get
            Set(value As Boolean)
                SetProperty(_isSeasonRelevant, value)
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

        Private _isImageDownloadEnabled As Boolean = True
        Public Property IsImageDownloadEnabled As Boolean
            Get
                Return _isImageDownloadEnabled
            End Get
            Set(value As Boolean)
                SetProperty(_isImageDownloadEnabled, value)
            End Set
        End Property

#End Region

        Protected Overrides Function CanDownloadImages() As Boolean
            Return Not String.IsNullOrWhiteSpace(ShowName) AndAlso
                Not String.IsNullOrWhiteSpace(PlaylistId)
        End Function

        Protected Overrides Sub DownloadImages()
            Dim tvdata As New TvDataSeries
            Dim pageToken As String = Nothing
            Using youTubeRateGate As New RateGate(10, TimeSpan.FromSeconds(1))
                Do
                    Dim json As String = String.Empty
                    Try
                        youTubeRateGate.WaitToProceed()
                        json = WebResources.DownloadString(GetPlaylistItemsUrl(pageToken))
                    Catch ex As Exception
                        MessageWindow.ShowDialog("Error while downloading json: " & ex.Message, "Error")
                        Exit Do
                    End Try
                    Dim response = json.FromJSON(Of PlaylistItemListResponse)()
                    pageToken = response.NextPageToken
                    For Each ep In response.Items
                        Dim tvEp = New TvDataEpisode() With {
                            .EpisodeName = ep.Snippet.Title,
                            .Overview = ep.Snippet.Description,
                            .ImageUrl = ep.BestImageUrl
                        }
                        If ep.ContentDetails IsNot Nothing Then
                            tvEp.FirstAired = ep.ContentDetails.VideoPublishedAt.ToIso8601DateString
                        End If

                        Select Case RetrievalMode
                            Case YouTubeRetrievalMode.ByYear
                                Dim dte As Date
                                If ep.ContentDetails IsNot Nothing AndAlso
                                   Date.TryParse(ep.ContentDetails.VideoPublishedAt, dte) Then
                                    tvEp.SeasonNumber = dte.Year
                                End If
                            Case YouTubeRetrievalMode.SingleSeasonUseListOrder
                                tvEp.EpisodeNumber = ep.Snippet.Position
                                tvEp.SeasonNumber = SeasonNumber.Value
                        End Select
                        tvdata.Episodes.Add(tvEp)
                    Next
                Loop Until pageToken = Nothing
            End Using

            ' sort episodes by FirstAired date and assign EpisodeNumber
            If RetrievalMode <> YouTubeRetrievalMode.SingleSeasonUseListOrder Then
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
            End If

            Parallel.ForEach(tvdata.Episodes, AddressOf DownloadImage)

            If tvdata.Episodes.Count > 0 Then
                Dim seasonName As String = String.Empty
                If RetrievalMode <> YouTubeRetrievalMode.ByYear Then
                    seasonName = " Season " & SeasonNumber.Value
                End If
                tvdata.SaveToFile(ShowDownloadFolder, ShowName & seasonName)
            Else
                MessageWindow.ShowDialog("No episodes were found", "No Episodes Found")
            End If
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
            If IsImageDownloadEnabled Then
                DownloadImageAddResult(episode.ImageUrl, localPath)
            Else
                AddEpisodeImageResult(New EpisodeImageResult() With {.FileName = SeasonEpisodeFilename, .HasError = True, .NewDownload = False, .Message = "Image Download Disabled"})
            End If
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
                Return New RelayCommand(
                    Sub()
                        Process.Start(ShowDownloadFolder())
                    End Sub, AddressOf ShowDownloadFolderExists)
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

#End Region

        Public Property RetrievalTypes As New Dictionary(Of String, YouTubeRetrievalMode) From {
            {"By Year", YouTubeRetrievalMode.ByYear},
            {"Single Season, Sort by date", YouTubeRetrievalMode.SingleSeasonSortByDate},
            {"Single Season, Use list order", YouTubeRetrievalMode.SingleSeasonUseListOrder}
        }

        Public Enum YouTubeRetrievalMode As Integer
            ByYear = 1
            SingleSeasonSortByDate = 2
            SingleSeasonUseListOrder = 3
        End Enum

#Region " Private Functions "

        Private Function GetPlaylistItemsUrl(pageToken As String) As String
            Dim format = "https://www.googleapis.com/youtube/v3/playlistItems?" &
                "maxResults=50&part=contentDetails%2Csnippet&key=AIzaSyAhixxV8Ms9I7ptRyZe_urI5SpWm9SzC0A" &
                "&fields=nextPageToken%2Citems(snippet(title%2Cdescription%2Cthumbnails%2Cposition)%2CcontentDetails(videoPublishedAt))" &
                "&playlistId={0}&pageToken={1}"
            Return String.Format(format, PlaylistId, pageToken)
        End Function

#End Region

#Region " JSON Data Structures "

        <DataContract>
        Public Class PlaylistItemListResponse

            <DataMember(Name:="nextPageToken")>
            Public Property NextPageToken As String

            <DataMember(Name:="items")>
            Public Property Items As List(Of Item)
        End Class

        <DataContract>
        Public Class Item

            <DataMember(Name:="snippet")>
            Public Property Snippet As Snippet

            <DataMember(Name:="contentDetails")>
            Public Property ContentDetails As ContentDetails

            Public ReadOnly Property BestImageUrl As String
                Get
                    If Snippet.Thumbnails Is Nothing Then
                        Return Nothing
                    End If
                    If Snippet.Thumbnails.Maxres IsNot Nothing AndAlso Not String.IsNullOrEmpty(Snippet.Thumbnails.Maxres.Url) Then
                        Return Snippet.Thumbnails.Maxres.Url
                    ElseIf Snippet.Thumbnails.High IsNot Nothing AndAlso Not String.IsNullOrEmpty(Snippet.Thumbnails.High.Url) Then
                        Return Snippet.Thumbnails.High.Url
                    ElseIf Snippet.Thumbnails.Medium IsNot Nothing AndAlso Not String.IsNullOrEmpty(Snippet.Thumbnails.Medium.Url) Then
                        Return Snippet.Thumbnails.Medium.Url
                    ElseIf Snippet.Thumbnails.Standard IsNot Nothing AndAlso Not String.IsNullOrEmpty(Snippet.Thumbnails.Standard.Url) Then
                        Return Snippet.Thumbnails.Standard.Url
                    Else
                        Return Nothing
                    End If
                End Get
            End Property
        End Class

        <DataContract>
        Public Class Snippet

            <DataMember(Name:="title")>
            Public Property Title As String

            <DataMember(Name:="description")>
            Public Property Description As String

            <DataMember(Name:="position")>
            Public Property Position As Integer

            <DataMember(Name:="thumbnails")>
            Public Property Thumbnails As Thumbnails
        End Class

        <DataContract>
        Public Class Thumbnails

            <DataMember(Name:="default")>
            Public Property [Default] As Thumbnail

            <DataMember(Name:="medium")>
            Public Property Medium As Thumbnail

            <DataMember(Name:="high")>
            Public Property High As Thumbnail

            <DataMember(Name:="standard")>
            Public Property Standard As Thumbnail

            <DataMember(Name:="maxres")>
            Public Property Maxres As Thumbnail
        End Class

        <DataContract>
        Public Class Thumbnail

            <DataMember(Name:="url")>
            Public Property Url As String

            <DataMember(Name:="width")>
            Public Property Width As Integer

            <DataMember(Name:="height")>
            Public Property Height As Integer
        End Class

        <DataContract>
        Public Class ContentDetails

            <DataMember(Name:="videoPublishedAt")>
            Public Property VideoPublishedAt As String
        End Class

#End Region

    End Class

End Namespace
